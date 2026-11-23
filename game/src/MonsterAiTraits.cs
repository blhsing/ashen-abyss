using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>The traits of BESTIARY.md 5.2 (Dormant … Reflect), combinable with any AI kind.</summary>
public partial class Monster
{
    // ═════════════════════════ Dormant 伏擊 ═════════════════════════

    private void DormantSetup()
    {
        _dormant = true;
        switch (A.DormantMode)
        {
            case "statue":
                // a figure among the figures: frozen in its pose, stone-grey, no bar until it is struck
                Model.Play(Def.Idle, 1f, 0f);
                Model.Pause(true);
                _buried = true;              // keeps Monster.Tick from un-pausing the pose
                Model.SetTint(Def.Tint * new Color(0.72f, 0.72f, 0.74f));
                break;
            case "ceiling":
                SetHidden(true);
                _dormantMark = MonsterFx.Shadow(this, Mathf.Max(0.5f, Radius * 1.3f));
                break;
            default:      // buried: a faint mound, the odd bubble or puff of dust
                SetHidden(true);
                _dormantMark = MonsterFx.Mound(this, Mathf.Max(0.5f, Radius * 1.4f), Def.Family == "mire");
                break;
        }
    }

    private void DormantIdle(float dist)
    {
        var p = World.P;
        if (_aggro || (dist < A.WakeRadius && p != null && !p.Dead && !p.Stealthed)) Wake(_aggro ? "pack" : "near");
    }

    private void DormantHint()
    {
        var p = World.P;
        if (p == null || Pos2.DistanceSquaredTo(p.Pos2) > 18f * 18f || !Rng.Chance(0.25f)) return;
        if (A.DormantMode == "buried") Fx.Dust(GlobalPosition);
    }

    private void Wake(string why)
    {
        if (!_dormant) return;
        _dormant = false;
        var p = World.P;
        bool wasHidden = AiHidden;
        if (_dormantMark != null && IsInstanceValid(_dormantMark)) _dormantMark.QueueFree();
        _dormantMark = null;
        if (A.DormantMode == "statue") { _buried = false; Model.Pause(false); Model.SetTint(Def.Tint); }
        _aggro = true;
        if (Pack >= 0) foreach (var m in World.Monsters) if (m.Pack == Pack && !m._aggro) m._aggro = true;
        _firstMul = 1.3f;
        _firstStatus = A.WakeStatus;
        Note("Dormant.wake", $"{A.DormantMode} ({why})");
        // the telegraph: at its own spot, or under the hero for a drop from the ceiling
        bool ceiling = A.DormantMode == "ceiling";
        Vector2 at = ceiling && p != null ? p.Pos2 : Pos2;
        var tele = Fx.Telegraph(at, 2f, 0.6f, Palette.Danger, Def.Element);
        Snd.At(Def.Sfx + "_idle", GlobalPosition, 0.9f);
        if (ceiling)
        {
            // drops onto where the hero stood
            Channel(0.6f, () =>
            {
                Pos2 = World.Grid?.NearestWalk(at) ?? at;
                SetHidden(false);
                Model.Play(Model.Has("Spawn") ? "Spawn" : "Hit_Knockback", 1.6f, 0f, true);
                World.InCircle(Faction.Monster, at, 2f, AiTmp);
                foreach (var a in AiTmp.ToArray()) Strike(a, AiFirstMul(), _firstStatus, false);
                _firstStatus = null;
                Fx.Burst(World.To3(at, 0.3f), Def.Element, 2f, 0.7f);
                Snd.At("leap_land", GlobalPosition, 0.8f);
                Note("Dormant.strike", "ceiling drop");
            }, null, () => SetHidden(false), false);
            return;
        }
        if (wasHidden) SetHidden(false);
        Model.Play(Model.Has("LayToIdle") && A.DormantMode == "buried" ? "LayToIdle" : Model.Has("Spawn") ? "Spawn" : Def.Idle, 1.8f, 0f, true);
        if (wasHidden) Fx.Dust(GlobalPosition);
        // its first attack follows at once: hit what stands in the circle when the 0.6 s are up
        Channel(0.6f, () =>
        {
            World.InCircle(Faction.Monster, at, 2f + Radius, AiTmp);
            foreach (var a in AiTmp.ToArray()) Strike(a, AiFirstMul(), _firstStatus, true);
            if (AiTmp.Count > 0) { Note("Dormant.strike"); Snd.At("hit_flesh", GlobalPosition, 0.7f); }
            _firstStatus = null;
            _cd = Mathf.Max(_cd, 0.6f);
        }, null, null, false);
    }

    // ═════════════════════════ Hook 鉤拉 ═════════════════════════

    private bool TryHook(float dist, bool sight)
    {
        if (_aiCd[CdHook] > 0f || dist < 4f || dist > A.HookRange || !sight || Rooted) return false;
        _aiCd[CdHook] = 8f;
        Vector2 dir = (_target.Pos2 - Pos2).Normalized();
        FaceDir(dir);
        _telegraph = Fx.TelegraphLine(Pos2, dir, A.HookRange, 1f, 0.6f, PullColor, Sigil.Blade);
        string clip = Def.Rig != "A" ? "Attack2" : CharModel.HasClip(A.CastAnim) && A.CastAnim != "Spell_Simple_Shoot" ? A.CastAnim : "OverhandThrow";
        Model.Play(clip, 1f, 0.1f, true);
        Snd.At("chain", GlobalPosition, 0.6f);
        Note("Hook.windup");
        Channel(0.6f, () =>
        {
            Vector2 end = Pos2 + dir * A.HookRange;
            if (World.Grid != null) end = World.Grid.ClampLine(Pos2, end, true);
            World.InCapsule(Faction.Monster, Pos2, end, 0.5f, AiTmp);
            Actor first = null; float bd = float.MaxValue;
            foreach (var a in AiTmp) { float d = a.Pos2.DistanceSquaredTo(Pos2); if (d < bd) { bd = d; first = a; } }
            Fx.Beam(Model.BonePos("hand_r"), first != null ? first.Chest : World.To3(end, 1f), Element.Physical, 0.08f, 0.35f);
            if (first == null) { Note("Hook.miss"); return; }
            float d0 = first.Pos2.DistanceTo(Pos2) - (Radius + first.Radius + 1.0f);
            float dealt = Strike(first, 0.6f, A.HookStatus, false);
            if (dealt > 0f && d0 > 0.3f) first.ApplyStatus(St.Pull(Mathf.Sqrt(56f * d0)), this);
            _cd = Mathf.Min(_cd, 0.3f);
            Snd.At("chain", first.GlobalPosition, 0.8f);
            Note("Hook.pull", $"{d0 + 1.5f:0.0} m");
        }, null, null);
        return true;
    }

    // ═════════════════════════ Grab 擒抱 ═════════════════════════

    private bool TryGrab(float dist, float reach)
    {
        if (_aiCd[CdGrab] > 0f || dist > reach + 0.3f || _target is not Player) return false;
        _aiCd[CdGrab] = 10f;
        float len = Def.Range + Radius + 1f;
        FaceDir(_target.Pos2 - Pos2);
        _telegraph = Fx.TelegraphCone(this, len, 70f, 0.7f, GrabColor, Sigil.Blade);
        Model.Play(Def.Rig == "A" ? "Melee_Hook" : "Attack2", 0.8f, 0.1f, true);
        Note("Grab.windup");
        Channel(0.7f, () =>
        {
            World.InCone(Faction.Monster, Pos2, Forward, len, 70f, AiTmp);
            var p = AiTmp.Find(a => a is Player) as Player;
            if (p == null || p.Invulnerable || p.Unstoppable) { Note("Grab.miss"); Snd.At("swing_heavy", GlobalPosition, 0.5f); return; }
            Strike(p, 0.3f, null, true);
            p.ApplyStatus(St.Root(A.HoldTime + 0.1f), this);
            _grabbed = p; _grabTaken = 0f; _grabTick = 0.5f;
            Note("Grab.hold", $"{A.HoldTime:0.0} s");
            Channel(A.HoldTime, () => GrabRelease("held"), null, () => GrabRelease("broken"));
        }, null, null);
        return true;
    }

    private float _grabTick;

    private void GrabHold(float dt)
    {
        var p = _grabbed;
        if (p == null || !IsInstanceValid(p) || p.Dead) { _grabbed = null; return; }
        Vector2 at = Pos2 + Forward * (Radius + p.Radius + 0.25f);
        if (World.Grid == null || World.Grid.WalkAt(at)) p.Pos2 = at;
        _grabTick -= dt;
        if (_grabTick <= 0f) { _grabTick = 0.5f; Strike(p, 0.3f, null, false); }
    }

    private void GrabRelease(string why)
    {
        var p = _grabbed;
        _grabbed = null;
        if (p == null || !IsInstanceValid(p)) return;
        p.StTime[(int)StatusId.Root] = 0f;
        Note("Grab.release", why);
        if (why == "held" && A.GrabThrow && !p.Dead)
        {
            // a giant finishes by throwing the hero into a marked circle
            Vector2 dir = Forward;
            // Knock 12 carries v²/(2·28) ≈ 2.6 m (Actor's knock decay): the circle is where the hero really lands
            Vector2 at = p.Pos2 + dir * (12f * 12f / 56f);
            Vector2 land = World.Grid?.NearestWalk(at) ?? at;
            Fx.Telegraph(land, 1.6f, 0.6f, GrabColor, Sigil.Blade);
            p.ApplyStatus(St.Knock(12f), this);
            Model.Play(Def.Rig == "A" ? "Sword_Attack" : "Attack2", 1.2f, 0.05f, true);
            var self = this;
            AshenAbyss.Level.Current?.After(0.6f, () =>
            {
                if (!IsInstanceValid(self) || !IsInstanceValid(p) || p.Dead) return;
                if (p.Pos2.DistanceTo(land) < 2.4f) Combat.MonsterHit(self, p, 0.8f, Def.Element);
                Fx.Burst(World.To3(p.Pos2, 0.2f), Element.Physical, 1.6f, 0.8f);
                Fx.Shake(0.3f, 0.2f);
                Snd.At("body_fall", p.GlobalPosition);
                self.Note("Grab.throw");
            });
        }
    }

    // ═════════════════════════ Breath 吹息 ═════════════════════════

    private bool TryBreath(float dist, bool sight)
    {
        bool replaces = Def.AI is MonAI.Caster or MonAI.Ranged;
        if (_aiCd[CdBreath] > 0f || !sight || Rooted && !replaces) return false;
        if (replaces ? dist > A.BreathLen : dist < 4f || dist > 9f) return false;
        if (Def.AI == MonAI.Burrower && _bur != 3) return false;
        _aiCd[CdBreath] = 7f;
        if (replaces) _cd = Mathf.Max(_cd, 2f);
        Vector2 dir = (_target.Pos2 - Pos2).Normalized();
        FaceDir(dir);
        _telegraph = Fx.TelegraphCone(this, A.BreathLen, A.BreathArc, 0.8f, Palette.Danger, Def.Element);
        Model.Play(Def.Rig != "A" ? "Attack2" : A.CastAnim, 0.7f, 0.1f, true);
        Snd.At("telegraph_warn", GlobalPosition, 0.4f);
        Note("Breath.windup");
        Element el = Def.Element;
        Channel(0.8f, () =>
        {
            int tick = 0; float next = 0f;
            var hits = new Dictionary<Actor, int>();
            Note("Breath.blow", A.BreathMode);
            Channel(2f, null, dt =>
            {
                next -= dt;
                if (next > 0f) return;
                next = 0.5f; tick++;
                World.InCone(Faction.Monster, Pos2, Forward, A.BreathLen, A.BreathArc, AiTmp);
                foreach (var a in AiTmp.ToArray())
                {
                    hits.TryGetValue(a, out int n); hits[a] = ++n;
                    StatusApply? st = A.BreathMode switch
                    {
                        "knock" => St.Knock(3f),
                        "chill" => n >= 4 ? St.Freeze(1f) : A.BreathStatus ?? St.Chill(0.3f * Bestiary.ChillScale),
                        _ => A.BreathStatus,
                    };
                    Strike(a, 0.35f, st, false, el);
                    if (A.BreathMode == "chill" && n >= 4) Note("Breath.freeze");
                }
                for (int i = 1; i <= 3; i++) Fx.Burst(World.To3(Pos2 + Forward * (A.BreathLen * i / 3.5f), 1f), el == Element.Physical ? Element.Fire : el, 0.8f + i * 0.5f, 0.5f);
                if (Def.Is(MonTrait.Extinguish)) Extinguish(Pos2 + Forward * A.BreathLen * 0.5f, A.BreathLen * 0.5f);
            }, null);
        }, null, null);
        return true;
    }

    // ═════════════════════════ Eruption 地擊 ═════════════════════════

    private bool TryErupt(float dist, bool sight)
    {
        if (_cd > 0f || dist > Def.Range || !sight || Has(StatusId.Fear)) return false;
        _cd = Def.Cooldown / (AtkSpeedMul * (_enrage > 0 ? 1.3f : 1f));
        Vector2 at = _target.Pos2;
        FaceDir(at - Pos2);
        _telegraph = Fx.Telegraph(at, A.EruptRadius, 0.9f, Palette.Danger, Def.Element);
        Model.Play(A.CastAnim, 1f, 0.1f, true);
        Snd.At(Def.Element == Element.Lightning ? "lightning_cast" : "earth_cast", GlobalPosition, 0.6f);
        Note("Eruption.cast");
        Channel(0.9f, () =>
        {
            World.InCircle(Faction.Monster, at, A.EruptRadius, AiTmp);
            foreach (var a in AiTmp.ToArray()) Strike(a, 1.1f * AiFirstMul(), null, false);
            Fx.Burst(World.To3(at, 0.2f), Def.Element, A.EruptRadius, 1f);
            Snd.At(Def.Element == Element.Lightning ? "storm_bolt" : "earth_impact", World.To3(at), 0.8f);
            if (A.EruptZone != null) AddZone(at, A.EruptZone, 3);
            Note("Eruption.hit");
        }, null, null);
        return true;
    }

    // ═════════════════════════ Shell 外殼 (state in MonsterAi.cs: barrier + OnBarrierEnd) ═════════════════════════

    // ═════════════════════════ Reform 復形 ═════════════════════════

    private bool InFrostZone()
    {
        foreach (var z in GroundZone.Live)
            if (IsInstanceValid(z) && !z.Done && z.El == Element.Frost && z.Center.DistanceTo(Pos2) <= z.Radius) return true;
        return false;
    }

    private void BecomePile()
    {
        _pile = true;
        Life = 1f;
        CancelTelegraph();
        Model.Play(Model.Has("Death01") ? "Death01" : "Hit_Knockback", 1.5f, 0.08f, true);
        Model.SetTint(Def.Tint * new Color(0.55f, 0.53f, 0.5f));
        Fx.Ash(this);
        Snd.At("ash_crumble", GlobalPosition, 0.8f);
        Note("Reform.pile");
        Channel(A.ReformTime, () =>
        {
            _pile = false; _reformed = true;
            Life = MaxLife * 0.5f;
            Model.SetTint(Def.Tint);
            Model.Play(Model.Has("LayToIdle") ? "LayToIdle" : Def.Idle, 1.6f, 0f, true);
            Busy(Model.Len("LayToIdle") / 1.6f, null);
            Fx.Dust(GlobalPosition);
            Note("Reform.stand");
        }, null, () => { }, false);
    }

    // ═════════════════════════ Flammable 易燃 ═════════════════════════

    private void FlammableTick(double now)
    {
        if (_lit)
        {
            // it burns away: 4 % of its life per second
            TakeHit(new Hit { Source = World.P, Amount = MaxLife * 0.02f, El = Element.Fire, Dot = true, NoReact = true });
            return;
        }
        foreach (var z in GroundZone.Live)
            if (IsInstanceValid(z) && !z.Done && z.El == Element.Fire && z.Center.DistanceTo(Pos2) <= z.Radius + Radius * 0.5f) { Ignite("burning ground"); return; }
    }

    private void Ignite(string why)
    {
        if (_lit || Dead || !Def.Is(MonTrait.Flammable)) return;
        _lit = true;
        _litFx = Fx.Fire(Mathf.Clamp(Def.Scale * 0.7f, 0.5f, 1.4f), true);
        AddChild(_litFx);
        _litFx.Position = new Vector3(0, HeightM * 0.55f, 0);
        Model.SetRim(Palette.Ember * 0.8f);
        Snd.At("fire_cast", GlobalPosition, 0.7f);
        Note("Flammable.lit", why);
    }

    private void PutOut(string why)
    {
        if (!_lit) return;
        _lit = false;
        if (_litFx != null && IsInstanceValid(_litFx)) _litFx.QueueFree();
        _litFx = null;
        Model.SetRim(BaseRim());
        Fx.Dust(GlobalPosition);
        Note("Flammable.out", why);
    }

    private void LitDeathPatch()
    {
        AddZone(Pos2, new MonZone { Radius = 1.6f, Dur = 2f, El = Element.Fire, Status = A.LitBurn, Mul = 0.1f }, 9);
        Note("Flammable.embers");
    }

    // ═════════════════════════ Aura 光環 ═════════════════════════

    private void AuraPulse(double now)
    {
        float r = A.AuraRadius;
        switch (A.AuraEffect)
        {
            case "haste":
            case "windup":
                int n = 0;
                foreach (var m in World.Monsters)
                {
                    if (m == this || m.Dead || m.Pos2.DistanceSquaredTo(Pos2) > r * r) continue;
                    if (A.AuraEffect == "windup") { if (!IsConstruct(m.Def)) continue; m._auraWindUntil = now + 0.8; }
                    else m._auraHasteUntil = now + 0.8;
                    n++;
                }
                if (n > 0) Note("Aura." + A.AuraEffect, $"{n} allies");
                break;
            case "chill":
            case "weaken":
                World.InCircle(Faction.Monster, Pos2, r, AiTmp);
                foreach (var a in AiTmp.ToArray())
                {
                    if (A.AuraEffect == "chill")
                    {
                        float cur = a.Has(StatusId.Chill) ? a.StMag[(int)StatusId.Chill] : 0f;
                        if (cur < 0.3f * Bestiary.ChillScale) a.ApplyStatus(St.Chill(0.3f * Bestiary.ChillScale - cur, 1.2f), this);
                    }
                    else ApplyHostile(a, St.Weaken(0.25f, 1.2f), Def.Element);
                }
                if (AiTmp.Count > 0) Note("Aura." + A.AuraEffect);
                break;
        }
    }

    /// <summary>The cage-bearer's stolen ember: walking over it gives 25 % resource and +15 % damage for 10 s.</summary>
    private void DropStolenEmber()
    {
        var at = World.Grid?.NearestWalk(Pos2) ?? Pos2;
        var it = WorldObjects.Make("ember", at, "竊燼", Palette.Ember, 1.4f, 0.8f);
        if (it == null) return;
        it.AutoRadius = 1.3f; it.ShowLabel = true;
        var fire = Fx.Fire(0.35f, true);
        it.AddChild(fire);
        fire.Position = new Vector3(0, 0.35f, 0);
        it.OnUse = p =>
        {
            if (p == null) return;
            p.GainResource(p.Hero.MaxResource * 0.25f);
            p.Hero.AddBuff(new BuffDef { Id = "stolen_ember", Name = "竊燼", Stat = Stat.DmgAll, Value = 15f }, 10f);
            Fx.BuffAura(p, Element.Fire, 10f, false);
            Snd.Ui("ember_buff", 0.8f);
            Log.Info("ai", "Aura.ember picked up: +25% resource, +15% damage for 10 s");
            AiSeen.TryGetValue("Aura.emberpickup", out int c); AiSeen["Aura.emberpickup"] = c + 1;
            it.Consume();
        };
        Note("Aura.emberdrop");
    }

    // ═════════════════════════ SplitOnDeath 分裂 ═════════════════════════

    private void SplitApart()
    {
        var def = LocalDef(A.SplitDef);
        if (def == null) return;
        int n = A.SplitCount, lv = Level, pack = Pack;
        Vector2 at = Pos2;
        Fx.Burst(Chest, Def.Element, 1.8f, 1f);
        Snd.At("body_fall", GlobalPosition, 0.8f);
        bool noReform = Def.Id.StartsWith("ash_") || Def.BaseId != null && Def.BaseId.StartsWith("ash_");
        float xp = XpValue;
        Note("SplitOnDeath.split", $"{n}×{def.Id}");
        AshenAbyss.Level.Current?.After(0.4f, () =>
        {
            if (World.Root == null) return;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = World.Grid?.RandomWalkNear(at, 0.6f, 2f) ?? at;
                var m = Monster.Spawn(def, lv, MonTier.Normal, p, Faction.Monster);
                m.Pack = pack; m.NoLoot = true; m.XpValue *= 0.5f; m._aggro = true; m.NoReform = noReform;
                Fx.Dust(World.To3(p));
            }
        });
    }

    // ═════════════════════════ DeathBurst 殞爆 ═════════════════════════

    private void DeathBurst()
    {
        float r = A.BurstRadius;
        float delay = Def.Role == "swarmer" || Def.Scale < 0.9f || A.BurstDelay < 0.5f ? 0.4f : 0.6f;
        Vector2 at = Pos2;
        Element el = A.BurstEl ?? Def.Element;
        Fx.Telegraph(at, r, delay, Palette.Danger, Def.Element);
        _keepUntil = Math.Max(_keepUntil, Now + delay + (A.BurstZone?.Dur ?? 0f) + 0.2);
        var self = this;
        Note("DeathBurst.armed");
        AshenAbyss.Level.Current?.After(delay, () =>
        {
            if (!IsInstanceValid(self)) return;
            World.InCircle(Faction.Monster, at, r, AiTmp);
            foreach (var a in AiTmp.ToArray()) Combat.MonsterHit(self, a, A.BurstDmg, el, A.BurstStatus);
            Fx.Burst(World.To3(at, 0.6f), el, r, 1.1f);
            Snd.At(el == Element.Lightning ? "thunder" : el == Element.Frost ? "frost_shatter" : el == Element.Poison ? "poison_splash" : "bloat_explode", World.To3(at), 0.9f);
            if (A.BurstZone != null) self.AddZone(at, A.BurstZone, 9);
            self.Note("DeathBurst.burst", $"r {r:0.0} {el}");
        });
    }

    // ═════════════════════════ zones (Bomber, Trail, bursts) ═════════════════════════

    /// <summary>A ground zone of this monster; at most <paramref name="cap"/> live at once (the oldest goes).</summary>
    private GroundZone AddZone(Vector2 at, MonZone z, int cap)
    {
        if (z == null || World.Root == null) return null;
        _zones.RemoveAll(g => g == null || !IsInstanceValid(g) || g.Done);
        while (_zones.Count >= cap) { var old = _zones[0]; _zones.RemoveAt(0); if (IsInstanceValid(old)) old.Remove(); }
        var g = GroundZone.SpawnMonster(this, at, z.Radius, z.Dur, z.Mul, z.El, z.Status);
        _zones.Add(g);
        if (z.RootOnReentry > 0f) _watch.Add((g, false, 0));
        _keepUntil = Math.Max(_keepUntil, Now + z.Dur + 0.2);
        return g;
    }

    /// <summary>The ground an attack of a Trail type leaves behind (frost patch, wax pool, burning wax, fire patches).</summary>
    private void AiTrailAt(Vector2 at)
    {
        if (!Def.Is(MonTrait.Trail) || A.Trail == null) return;
        AddZone(at, A.Trail, Def.AI == MonAI.Flyer ? 6 : 3);
        Note("Trail.zone");
    }

    private void ZonesTick()
    {
        _zones.RemoveAll(g => g == null || !IsInstanceValid(g) || g.Done);
        var p = World.P;
        for (int i = _watch.Count - 1; i >= 0; i--)
        {
            var (z, inside, entries) = _watch[i];
            if (z == null || !IsInstanceValid(z) || z.Done) { _watch.RemoveAt(i); continue; }
            bool now = p != null && !p.Dead && p.Pos2.DistanceTo(z.Center) <= z.Radius;
            if (now && !inside)
            {
                entries++;
                // the tallow pool: a second entry roots
                if (entries >= 2 && A.Trail != null && A.Trail.RootOnReentry > 0f) { p.ApplyStatus(St.Root(A.Trail.RootOnReentry), this); Note("Trail.root"); }
            }
            _watch[i] = (z, now, entries);
        }
    }

    // ═════════════════════════ Extinguish 熄滅 ═════════════════════════

    private static readonly List<(Vector2 at, float r, double until)> NoFire = new();

    /// <summary>Within an attack's area: fire zones go out (the hero's too), burning allies are put out, no new fire for 2 s.</summary>
    public void Extinguish(Vector2 at, float r)
    {
        if (!Def.Is(MonTrait.Extinguish)) return;
        int zones = 0, allies = 0;
        foreach (var z in GroundZone.Live.ToArray())
            if (IsInstanceValid(z) && !z.Done && z.El == Element.Fire && z.Center.DistanceTo(at) <= r + z.Radius * 0.5f) { z.Remove(); zones++; }
        foreach (var m in World.Monsters)
        {
            if (m.Dead || m.Pos2.DistanceSquaredTo(at) > r * r) continue;
            bool had = m.Has(StatusId.Burn) || m._lit;
            m.Dots.RemoveAll(d => d.Id == StatusId.Burn); m.StTime[(int)StatusId.Burn] = 0f;
            m.PutOut("snuffed");
            if (had) allies++;
        }
        NoFire.RemoveAll(n => n.until < Now);
        NoFire.Add((at, r, Now + 2.0));
        Fx.Burst(World.To3(at, 0.4f), Element.Shadow, Mathf.Min(r, 3f), 0.4f);
        Note("Extinguish.snuff", $"{zones} fire zones, {allies} allies");
    }

    /// <summary>Called by GroundZone when it enters the tree: a fire zone born where the Order just snuffed the flames goes out.</summary>
    public static void ZoneBorn(GroundZone z)
    {
        if (NoFire.Count == 0 || z == null) return;
        Callable.From(() =>
        {
            if (!IsInstanceValid(z) || z.Done || z.El != Element.Fire) return;
            double now = Now;
            foreach (var n in NoFire)
                if (n.until >= now && n.at.DistanceTo(z.Center) <= n.r) { z.Remove(); AiSeen.TryGetValue("Extinguish.prevent", out int c); AiSeen["Extinguish.prevent"] = c + 1; return; }
        }).CallDeferred();
    }

    /// <summary>True when burning ground lies within <paramref name="r"/> of a point (for the named-encounter layer too).</summary>
    public static bool FireNear(Vector2 p, float r)
    {
        foreach (var z in GroundZone.Live) if (IsInstanceValid(z) && !z.Done && z.El == Element.Fire && z.Center.DistanceTo(p) <= r + z.Radius) return true;
        return false;
    }

    // ═════════════════════════ Drain 汲取 ═════════════════════════

    private static double _lastDrain = -10;
    private void DrainHero(Player p)
    {
        // one drain per 0.8 s whoever bites: a swarm of drainers emptied the bar in a second and the hero fought on with no skills
        double now = Game.I?.Time ?? 0;
        if (now - _lastDrain < 0.8 && now >= _lastDrain) return;
        _lastDrain = now;
        float before = p.Resource;
        p.Resource = MathF.Max(0f, p.Resource - p.Hero.MaxResource * A.DrainPct);
        if (A.DrainHeal) Heal(MaxLife * 0.05f);
        if (before > p.Resource) { Fx.Beam(p.Chest, Chest, Element.Shadow, 0.08f, 0.25f); Note("Drain.drain", $"{before - p.Resource:0} resource"); }
    }

    // ═════════════════════════ Pulse 脈衝 ═════════════════════════

    private bool TryPulse(float dist)
    {
        if (_aiCd[CdPulse] > 0f || dist > 9f) return false;
        Model.Play(Def.Rig != "A" ? "Attack2" : A.CastAnim, 0.9f, 0.1f, true);
        StartPulse();
        Busy(0.8f, null);
        return true;
    }

    private void StartPulse()
    {
        _aiCd[CdPulse] = 9f;
        Vector2 at = Pos2;
        Fx.Telegraph(at, 1.2f, 0.8f, Palette.Danger, Def.Element);
        Snd.At("bell_toll", GlobalPosition, 0.8f);
        Note("Pulse.mark", $"{A.PulseRings} ring(s)");
        var self = this;
        for (int i = 0; i < A.PulseRings; i++)
        {
            float delay = 0.8f + i * 0.7f;
            AshenAbyss.Level.Current?.After(delay, () =>
            {
                if (!IsInstanceValid(self) || self.Dead) return;
                if (A.PulseNear.HasValue)
                {
                    World.InCircle(Faction.Monster, at, A.PulseNearRadius, AiTmp);
                    foreach (var a in AiTmp.ToArray()) self.ApplyHostile(a, A.PulseNear.Value, Def.Element);
                }
                PulseRing.Start(self, at, 9f, 8f, 1.5f, 0.8f, A.PulseStatus);
                if (Def.Is(MonTrait.Extinguish)) self.Extinguish(at, 9f);
                self.Note("Pulse.ring");
            });
        }
        _keepUntil = Math.Max(_keepUntil, Now + 0.8 + A.PulseRings * 0.7 + 1.3);
    }

    // ═════════════════════════ Absorb 吞噬 ═════════════════════════

    /// <summary>Monsters with an open absorbing disc (Projectile asks while the hero's shots fly).</summary>
    public static readonly List<Monster> Absorbers = new();
    private Vector2 _discAt;
    private int _absorbed;

    private bool TryAbsorb(float dist)
    {
        bool shot = Now - _absorbWant < 2.0;
        if (_aiCd[CdAbsorb] > 0f || !(shot || (dist >= 5f && dist <= 12f))) return false;
        _aiCd[CdAbsorb] = 10f;
        FaceDir(_target.Pos2 - Pos2);
        _discAt = Pos2 + Forward * (A.AbsorbRadius * 0.6f);
        var disc = Fx.Telegraph(_discAt, A.AbsorbRadius, 0.6f, new Color(0.25f, 0.1f, 0.35f));
        Model.Play(A.CastAnim, 0.6f, 0.1f, true);
        Snd.At("shadow_cast", GlobalPosition, 0.8f);
        Note("Absorb.open");
        Channel(0.6f, () =>
        {
            _absorbed = 0;
            Absorbers.Add(this);
            float pull = 0f;
            var ring = MonsterFx.GroundRing(this, A.AbsorbRadius, new Color(0.35f, 0.15f, 0.5f, 0.5f));
            if (IsInstanceValid(ring)) { ring.TopLevel = true; ring.GlobalPosition = World.To3(_discAt, 0.06f); }
            void Close() { Absorbers.Remove(this); if (IsInstanceValid(ring)) ring.QueueFree(); }
            Channel(2.5f, () =>
            {
                Close();
                float mul = MathF.Min(1.5f, 0.5f + 0.15f * _absorbed);
                Note("Absorb.close", $"{_absorbed} shots");
                Fx.TelegraphRing(Pos2, 0f, 6f, 0.6f, Palette.Danger, Sigil.Shadow);
                Channel(0.6f, () =>
                {
                    World.InCircle(Faction.Monster, Pos2, 6f, AiTmp);
                    foreach (var a in AiTmp.ToArray()) Strike(a, mul, null, false, Element.Shadow);
                    Fx.Burst(Chest, Element.Shadow, 6f, 1.2f);
                    Snd.At("shadow_impact", GlobalPosition);
                    Note("Absorb.burst", $"{mul:0.00}x");
                }, null, null);
            }, dt =>
            {
                pull -= dt;
                if (pull > 0f) return;
                pull = 0.5f;
                World.InCircle(Faction.Monster, _discAt, A.AbsorbRadius, AiTmp);
                foreach (var a in AiTmp.ToArray()) if (a.Pos2.DistanceTo(Pos2) > 1.8f) a.ApplyStatus(St.Pull(3f * 2.4f), this);
            }, Close);
        }, null, () => { if (IsInstanceValid(disc)) disc.QueueFree(); });
        return true;
    }

    /// <summary>Projectile hook: a hero's shot inside an open disc is swallowed (counted for the burst).</summary>
    public static bool AbsorbShot(Vector2 at)
    {
        for (int i = Absorbers.Count - 1; i >= 0; i--)
        {
            var m = Absorbers[i];
            if (m == null || !IsInstanceValid(m) || m.Dead) { Absorbers.RemoveAt(i); continue; }
            if (m._discAt.DistanceTo(at) <= m.A.AbsorbRadius)
            {
                m._absorbed++;
                Fx.Hit(World.To3(at, 1f), Element.Shadow, false);
                m.Note("Absorb.swallow");
                return true;
            }
        }
        return false;
    }

    // ═════════════════════════ Shieldbearer / Reflect: shots against the guard ═════════════════════════

    /// <summary>Projectile hook: a hero's shot arriving at a raised shield is destroyed, or sent back (Reflect). True when consumed.</summary>
    public bool InterceptShot(Vector2 shotDir, Vector2 at)
    {
        if (!_ai || Def.AI != MonAI.Shieldbearer || !_guardUp || Dead) return false;
        if (!InFront(-shotDir.Normalized(), A.GuardArc)) return false;
        if (Now > _blockWindow) _blocks = 0;
        _blocks++; _blockWindow = Now + 4.0;
        Fx.Hit(World.To3(at, 1.2f), Element.Physical, false);
        Snd.At("shield_block", GlobalPosition, 0.7f);
        if (Def.Is(MonTrait.Reflect) && World.P != null && !World.P.Dead)
        {
            Vector2 back = (World.P.Pos2 - Pos2).Normalized();
            Projectile.SpawnMonster(this, Chest + new Vector3(back.X, 0, back.Y) * 0.6f, back, 18f, 16f, A.ReflectPct, Element.Lightning, null);
            Model.Flash(new Color(1f, 0.9f, 0.5f), 1.4f);
            Note("Reflect.reflect");
        }
        else Note("Shieldbearer.blockshot");
        return true;
    }

    // ═════════════════════════ Empower 加持 ═════════════════════════

    private bool TryEmpower()
    {
        if (_aiCd[CdEmpower] > 0f) return false;
        Monster ally = null; float bd = 100f;
        double now = Now;
        foreach (var m in World.Monsters)
        {
            if (m == this || m.Dead || !m._aggro || m.IsBoss || m.AiHidden || now < m._maskUntil || m._maskRaised) continue;
            float d = m.Pos2.DistanceSquaredTo(Pos2);
            if (d < bd) { bd = d; ally = m; }
        }
        if (ally == null) { _aiCd[CdEmpower] = 1f; return false; }
        _aiCd[CdEmpower] = 10f;
        var a = ally;
        FaceDir(a.Pos2 - Pos2);
        Model.Play(A.CastAnim, 1f, 0.1f, true);
        float beamT = 0f;
        Note("Empower.cast", a.Def.Id);
        Channel(1f, () =>
        {
            if (!IsInstanceValid(a) || a.Dead) return;
            a._maskUntil = Now + 12.0;
            a.MaskVisual(true);
            Snd.At("barrier_up", a.GlobalPosition, 0.7f);
            Note("Empower.mask", a.Def.Id);
        }, dt =>
        {
            if (!IsInstanceValid(a) || a.Dead) { CancelChannel("lost ally"); return; }
            beamT -= dt;
            if (beamT <= 0f) { beamT = 0.25f; Fx.Beam(Chest, a.Chest, Element.Lightning, 0.1f, 0.3f); }
        }, null);
        return true;
    }

    private void MaskVisual(bool on)
    {
        if (on)
        {
            _maskFx ??= MonsterFx.Mask(this);
            if (IsInstanceValid(_maskFx)) _maskFx.Visible = true;
            Model.SetRim(new Color(1f, 0.78f, 0.35f) * 0.8f);
            var self = this;
            AshenAbyss.Level.Current?.After(12f, () => { if (IsInstanceValid(self) && !self.Dead && Now >= self._maskUntil) self.MaskVisual(false); });
        }
        else
        {
            if (_maskFx != null && IsInstanceValid(_maskFx)) _maskFx.Visible = false;
            if (!_shellUp && !_lit) Model.SetRim(BaseRim());
        }
    }
}
