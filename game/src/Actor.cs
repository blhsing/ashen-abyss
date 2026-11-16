using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Animation self-check, always on (a few comparisons per moving actor per frame). It counts the seconds actors spent
/// moving under their own power while no locomotion clip was really advancing on their model, or with a leg pose that
/// did not change ("sliding"), and the seconds the hero stood still while a locomotion clip kept running. The test
/// harness prints it in every final summary: the missing-run-animation bug of the first build would have shown here.
/// </summary>
public static class AnimCheck
{
    public static double HeroMove, HeroSlide, HeroFrozen, HeroIdle, HeroIdleWrong, MonMove, MonSlide, MonFrozen;
    public static string HeroSlideClip = "", MonSlideClip = "", HeroIdleClip = "";

    public sealed class Probe { public float Sig = float.NaN, T, Still; public double Last; }

    /// <summary>One frame of an actor that is walking or running. Returns true when the frame counted as sliding.</summary>
    public static bool Moving(CharModel m, Probe p, float dt, bool hero, string who)
    {
        if (m == null) return false;
        if (hero) HeroMove += dt; else MonMove += dt;
        bool ok = m.Playing && CharModel.IsLocomotion(m.Current);
        if (!ok)
        {
            if (hero) { HeroSlide += dt; HeroSlideClip = $"{who}:{(m.Current.Length > 0 ? m.Current : "(none)")}{(m.Playing ? "" : " (not playing)")}"; }
            else { MonSlide += dt; MonSlideClip = $"{who}:{(m.Current.Length > 0 ? m.Current : "(none)")}{(m.Playing ? "" : " (not playing)")}"; }
            return true;
        }
        // the clip is right: is the skeleton really moving? sample a leg every 0.2 s of unbroken movement (a walker that
        // stops and starts restarts its clip, and would show the same early frame at every sample)
        double now = Game.I?.Time ?? 0.0;
        if (now - p.Last > 0.12 || now < p.Last) { p.Sig = float.NaN; p.T = 0f; p.Still = 0f; }
        p.Last = now;
        p.T += dt;
        if (p.T >= 0.2f)
        {
            float sig = m.LegSignature();
            if (!float.IsNaN(p.Sig) && Mathf.Abs(sig - p.Sig) < 1e-4f)
            {
                p.Still += p.T;
                if (p.Still >= 0.6f)         // three identical samples in a row: the legs are frozen
                {
                    if (hero) { HeroFrozen += p.T; HeroSlideClip = $"{who}:{m.Current} (pose frozen)"; }
                    else { MonFrozen += p.T; MonSlideClip = $"{who}:{m.Current} (pose frozen)"; }
                }
            }
            else p.Still = 0f;
            p.Sig = sig; p.T = 0f;
        }
        return false;
    }

    /// <summary>One frame of the hero standing still and free to act.</summary>
    public static void HeroStanding(CharModel m, float dt)
    {
        if (m == null) return;
        HeroIdle += dt;
        if (CharModel.IsLocomotion(m.Current) || m.Current.Length == 0) { HeroIdleWrong += dt; HeroIdleClip = m.Current.Length > 0 ? m.Current : "(none)"; }
    }

    public static string Report() =>
        $"anim-check: hero moved {HeroMove:0.0}s — {HeroSlide:0.0}s without a locomotion clip{(HeroSlide > 0.05 ? $" [{HeroSlideClip}]" : "")}, {HeroFrozen:0.0}s with frozen legs; " +
        $"hero stood {HeroIdle:0.0}s — {HeroIdleWrong:0.0}s in a run clip or none{(HeroIdleWrong > 0.05 ? $" [{HeroIdleClip}]" : "")}; " +
        $"monsters moved {MonMove:0.0}s — {MonSlide:0.0}s without a locomotion clip{(MonSlide > 0.05 ? $" [{MonSlideClip}]" : "")}, {MonFrozen:0.0}s with frozen legs; " +
        $"models without a skeleton or animation: {CharModel.Unanimated}";

    public static bool Clean => HeroSlide < 0.3 && HeroFrozen < 0.7 && HeroIdleWrong < 0.5 && MonSlide < Math.Max(1.0, MonMove * 0.01) && MonFrozen < Math.Max(1.0, MonMove * 0.01) && CharModel.Unanimated == 0;

    public static void Reset() { HeroMove = HeroSlide = HeroFrozen = HeroIdle = HeroIdleWrong = MonMove = MonSlide = MonFrozen = 0; HeroSlideClip = MonSlideClip = HeroIdleClip = ""; }
}

public struct Hit
{
    public Actor Source;
    public float Amount;
    public Element El;
    public bool Crit, Overpower, Dot, Minion, NoReact;
    public SkillDef Skill;
    public Vector2 Dir;
}

public sealed class DotEffect
{
    public StatusId Id;
    public Element El;
    public float Dps, Left;
    public Actor Source;
    public bool Minion;
}

/// <summary>
/// Base of everything that fights: position on the navigation grid (no physics bodies — movement is resolved against
/// the level grid, which keeps hundreds of actors cheap), life, barrier, statuses and damage intake.
/// </summary>
public partial class Actor : Node3D
{
    public Faction Faction;
    public string DisplayName = "";
    public int Level = 1;
    public float Life = 1, MaxLife = 1, Barrier, BarrierLeft;
    public float Radius = 0.5f, HeightM = 1.8f;
    public bool Dead, Untargetable;
    public CharModel Model;
    public float Facing;                       // yaw, radians; 0 faces +Z
    public Vector2 Knock;                      // knockback velocity, decays
    public readonly float[] StTime = new float[(int)StatusId.Count];
    public readonly float[] StMag = new float[(int)StatusId.Count];
    public readonly List<DotEffect> Dots = new();
    public float CcImmune;                     // after a hard CC ends, brief immunity so enemies can't be perma-locked
    public float HexCoef; public Actor HexSource;
    public Actor TauntBy;
    /// <summary>When this actor last took a tick from a ground zone of each skill (zones of one skill do not stack).</summary>
    public Dictionary<string, double> ZoneTicks;
    private float _dotTick;

    public Vector2 Pos2 { get => new(GlobalPosition.X, GlobalPosition.Z); set => GlobalPosition = new Vector3(value.X, GlobalPosition.Y, value.Y); }
    public Vector3 Chest => GlobalPosition + Vector3.Up * (HeightM * 0.6f);
    public Vector2 Forward => new(Mathf.Sin(Facing), Mathf.Cos(Facing));

    public bool Has(StatusId s) => StTime[(int)s] > 0f;
    public bool Stunned => Has(StatusId.Stun) || Has(StatusId.Freeze);
    public bool Rooted => Has(StatusId.Root) || Stunned;
    public bool HardCc => Stunned || Has(StatusId.Root) || Has(StatusId.Fear);
    public bool AnyCc => HardCc || Has(StatusId.Slow) || Has(StatusId.Chill);
    public virtual bool Unstoppable => false;
    public virtual bool IsElite => false;
    public virtual bool IsBoss => false;
    public float SlowMul => 1f - Mathf.Clamp(Mathf.Max(StMag[(int)StatusId.Slow] * (Has(StatusId.Slow) ? 1f : 0f), StMag[(int)StatusId.Chill] * 0.6f * (Has(StatusId.Chill) ? 1f : 0f)), 0f, 0.8f);
    public float WeakenMul => Has(StatusId.Weaken) ? 1f - StMag[(int)StatusId.Weaken] : 1f;

    public void FaceDir(Vector2 dir, float lerp = 1f)
    {
        if (dir.LengthSquared() < 1e-6f) return;
        float target = Mathf.Atan2(dir.X, dir.Y);
        Facing = lerp >= 1f ? target : Mathf.LerpAngle(Facing, target, lerp);
        Rotation = new Vector3(0, Facing, 0);
    }

    /// <summary>Moves on the grid with wall/obstacle resolution. Returns the actual displacement.</summary>
    public Vector2 Move(Vector2 delta)
    {
        var grid = World.Grid;
        Vector2 from = Pos2, to = from + delta;
        if (grid != null) to = grid.ResolveCircle(to, Radius, from);
        Pos2 = to;
        return to - from;
    }

    // ───────────────────────── statuses ─────────────────────────

    public virtual void ApplyStatus(StatusApply s, Actor source, float dotDps = 0f, Element dotEl = Element.Fire, bool minion = false)
    {
        if (Dead) return;
        if (s.Chance < 1f && !Rng.Chance(s.Chance)) return;
        int i = (int)s.Id;
        switch (s.Id)
        {
            case StatusId.Burn: case StatusId.Poison: case StatusId.Bleed: case StatusId.Rot:
                if (dotDps <= 0) return;
                DotEffect d = null;
                foreach (var e in Dots) if (e.Id == s.Id && e.Source == source) { d = e; break; }
                if (d == null) { d = new DotEffect { Id = s.Id, Source = source }; Dots.Add(d); }
                d.Dps = Mathf.Max(d.Dps, dotDps); d.Left = Mathf.Max(d.Left, s.Dur); d.El = dotEl; d.Minion = minion;
                StTime[i] = Mathf.Max(StTime[i], s.Dur);
                return;
            case StatusId.Knockback: case StatusId.Pull:
                if (Unstoppable || IsBoss || source == null) return;
                Vector2 dir = Pos2 - source.Pos2;
                if (dir.LengthSquared() < 0.01f) dir = source.Forward;
                dir = dir.Normalized();
                if (s.Id == StatusId.Pull) { if (Pos2.DistanceTo(source.Pos2) < 1.6f) return; Knock = -dir * s.Mag; }
                else Knock = dir * s.Mag;
                return;
            case StatusId.Hex:
                HexCoef = s.Mag; HexSource = source; StTime[i] = Mathf.Max(StTime[i], s.Dur);
                return;
            case StatusId.Chill:
                if (Unstoppable) return;
                StTime[i] = Mathf.Max(StTime[i], s.Dur);
                StMag[i] = Mathf.Min(1f, StMag[i] + s.Mag);
                if (StMag[i] >= 1f && CcImmune <= 0f && !IsBoss) { StMag[i] = 0.3f; ApplyStatus(St.Freeze(2f), source); }
                return;
            case StatusId.Taunt:
                TauntBy = source; StTime[i] = s.Dur; return;
        }
        bool hard = s.Id is StatusId.Stun or StatusId.Freeze or StatusId.Root or StatusId.Fear;
        if (hard)
        {
            if (Unstoppable) return;
            if (IsBoss) { OnStagger(s.Dur * 12f); return; }       // bosses convert crowd control into stagger
            if (CcImmune > 0f) return;
            float dur = s.Dur * (IsElite ? 0.6f : 1f);
            if (StTime[i] <= 0f) OnCcStart(s.Id);
            StTime[i] = Mathf.Max(StTime[i], dur);
            return;
        }
        if ((s.Id is StatusId.Slow) && Unstoppable) return;
        StTime[i] = Mathf.Max(StTime[i], s.Dur);
        StMag[i] = Mathf.Max(StMag[i], s.Mag);
        if (IsBoss && s.Id is StatusId.Slow) OnStagger(s.Dur * 2f);
    }

    protected virtual void OnCcStart(StatusId id) { }
    protected virtual void OnStagger(float amount) { }

    public void ClearCc()
    {
        foreach (var id in new[] { StatusId.Stun, StatusId.Freeze, StatusId.Root, StatusId.Fear, StatusId.Slow, StatusId.Chill })
        { StTime[(int)id] = 0; StMag[(int)id] = 0; }
        Model?.SetFrozen(0);
    }

    protected void TickStatuses(float dt)
    {
        bool wasHard = Stunned || Has(StatusId.Root);
        for (int i = 0; i < StTime.Length; i++)
        {
            if (StTime[i] <= 0f) continue;
            StTime[i] -= dt;
            if (StTime[i] <= 0f) { StTime[i] = 0f; StMag[i] = 0f; if (i == (int)StatusId.Taunt) TauntBy = null; }
        }
        if (wasHard && !(Stunned || Has(StatusId.Root))) CcImmune = IsElite ? 3f : 1.5f;
        if (CcImmune > 0f) CcImmune -= dt;
        Model?.SetFrozen(Has(StatusId.Freeze) ? 1f : Has(StatusId.Chill) ? StMag[(int)StatusId.Chill] * 0.5f : 0f);

        if (Dots.Count > 0)
        {
            _dotTick += dt;
            for (int i = Dots.Count - 1; i >= 0; i--) { Dots[i].Left -= dt; if (Dots[i].Left <= 0) Dots.RemoveAt(i); }
            if (_dotTick >= 0.5f)
            {
                _dotTick = 0f;
                for (int i = Dots.Count - 1; i >= 0 && !Dead; i--)
                {
                    var d = Dots[i];
                    TakeHit(new Hit { Source = d.Source, Amount = d.Dps * 0.5f, El = d.El, Dot = true, Minion = d.Minion, NoReact = true });
                }
            }
        }
        if (BarrierLeft > 0f) { BarrierLeft -= dt; if (BarrierLeft <= 0f) { Barrier = 0; OnBarrierEnd(); } }
        if (Knock.LengthSquared() > 0.01f)
        {
            Move(Knock * dt);
            Knock = Knock.MoveToward(Vector2.Zero, 28f * dt);
        }
    }

    protected virtual void OnBarrierEnd() { }

    public void AddBarrier(float amount, float dur)
    {
        Barrier = Mathf.Min(MaxLife, Mathf.Max(Barrier, 0) + amount);
        BarrierLeft = Mathf.Max(BarrierLeft, dur);
    }

    public virtual void Heal(float amount)
    {
        if (Dead || amount <= 0) return;
        Life = Mathf.Min(MaxLife, Life + amount);
    }

    // ───────────────────────── damage ─────────────────────────

    /// <summary>Applies an already-computed hit (mitigation is done by the overriding class). Returns damage to life.</summary>
    public virtual float TakeHit(Hit h)
    {
        if (Dead || h.Amount <= 0f) return 0f;
        float dmg = h.Amount;
        if (Barrier > 0f)
        {
            float absorbed = Mathf.Min(Barrier, dmg);
            Barrier -= absorbed; dmg -= absorbed;
            if (Barrier <= 0f) { BarrierLeft = 0; OnBarrierEnd(); }
        }
        Life -= dmg;
        World.RaiseDamage(this, h, h.Amount);
        if (Life <= 0f && !Dead)
        {
            if (!PreventDeath(h)) { Life = 0; Dead = true; Die(h); }
        }
        return dmg;
    }

    protected virtual bool PreventDeath(Hit h) => false;
    public virtual void Die(Hit h) { }
}
