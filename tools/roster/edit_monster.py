import sys
p = r'Y:\game\src\Monster.cs'
s = open(p, encoding='utf-8').read()


def rep(old, new, count=1):
    global s
    if s.count(old) != count:
        sys.exit(f"pattern count {s.count(old)} != {count}: {old[:100]}")
    s = s.replace(old, new)


rep("    public override bool Unstoppable => _st == Mode.Spawn || (Tier == MonTier.Minion && Def.Poise > 50);",
    "    public override bool Unstoppable => _st == Mode.Spawn || (Tier == MonTier.Minion && Def.Poise > 50) || AiUnstoppable;")
rep("""        else Model.Play(Def.Idle);
    }
""", """        else Model.Play(Def.Idle);
        Log.Guard("monster-ai", AiSetup);          // bestiary behaviours (MonsterAi*.cs); nothing for the built-in kinds
    }
""")
rep("""            if (_sleep != far) { _sleep = far; Model.Visible = far == 1; Model.SetAsleep(true); }
            return;
        }
        if (_sleep != 0) { _sleep = 0; Model.Visible = true; Model.SetAsleep(false); }
        TickStatuses(dt);
        if (Dead) return;""", """            if (_sleep != far) { _sleep = far; Model.Visible = far == 1 && !AiHidden; Model.SetAsleep(true); }
            return;
        }
        if (_sleep != 0) { _sleep = 0; Model.Visible = !AiHidden; Model.SetAsleep(false); }
        TickStatuses(dt);
        if (Dead) return;
        float facing0 = Facing;
        AiTickAlways(dt);
        if (Dead) return;""")
rep("""        if (Stunned) { Model.Pause(Has(StatusId.Freeze)); if (!Has(StatusId.Freeze)) Model.Play("Hit_Head", 0.6f, 0.1f); CancelTelegraph(); if (_st is Mode.Windup) _st = Mode.Chase; return; }""",
    """        if (Stunned) { AiOnStunned(); Model.Pause(Has(StatusId.Freeze)); if (!Has(StatusId.Freeze) && !AiHidden) Model.Play("Hit_Head", 0.6f, 0.1f); CancelTelegraph(); if (_st is Mode.Windup) _st = Mode.Chase; return; }""")
rep("""            case Mode.Busy: _t -= dt; if (_t <= 0) { _st = Mode.Chase; var e = _busyEnd; _busyEnd = null; e?.Invoke(); } break;
        }
    }""", """            case Mode.Busy: _t -= dt; if (_t <= 0) { _st = Mode.Chase; var e = _busyEnd; _busyEnd = null; _busyCancel = null; e?.Invoke(); } break;
        }
        AiPostTick(dt, facing0);
    }""")
rep("""    private void TickIdle(float dt, float distToPlayer)
    {
        Model.Play(Def.Idle, 1f, 0.3f);""", """    private void TickIdle(float dt, float distToPlayer)
    {
        if (AiIdle(dt, distToPlayer)) return;
        Model.Play(Def.Idle, 1f, 0.3f);""")
rep("""    private float MoveSpeed => Def.Speed * SpeedMul * SlowMul * (_enrage > 0 ? 1.2f : 1f);
    private float AtkSpeed => Def.AnimSpeed * AtkSpeedMul * (_enrage > 0 ? 1.3f : 1f) * Mathf.Lerp(1f, 0.6f, 1f - SlowMul);""",
    """    private float MoveSpeed => AiBaseSpeed * SpeedMul * SlowMul * (_enrage > 0 ? 1.2f : 1f) * AiSpeedMul;
    private float AtkSpeed => Def.AnimSpeed * AtkSpeedMul * (_enrage > 0 ? 1.3f : 1f) * Mathf.Lerp(1f, 0.6f, 1f - SlowMul) * AiAtkMul;""")
rep("""        Vector2 to = _target.Pos2 - Pos2;
        float dist = to.Length();
        float reach = Def.Range + (IsRanged ? 0f : Radius + _target.Radius - 0.5f);
        bool los = !IsRanged || World.Grid == null || World.Grid.LineShot(Pos2, _target.Pos2);

        if (TrySpecial(dist, los)) return;""", """        Vector2 to = _target.Pos2 - Pos2;
        float dist = to.Length();
        float reach = Def.Range + (IsRanged ? 0f : Radius + _target.Radius - 0.5f);
        bool los = !IsRanged || World.Grid == null || World.Grid.LineShot(Pos2, _target.Pos2);

        if (Faction == Faction.Monster && AiChase(dt, dist, los, reach)) return;
        if (TrySpecial(dist, los)) return;""")
rep("""    private bool IsRanged => Def.AI is MonAI.Ranged or MonAI.Caster or MonAI.Teleporter or MonAI.Support;""",
    """    private bool IsRanged => Def.AI is MonAI.Ranged or MonAI.Caster or MonAI.Teleporter or MonAI.Support or MonAI.Bomber || (Def.AI == MonAI.Caller && Def.Range > 6f);""")
rep("""        Vector2 sep = SeparationPush() * 2.2f;""", """        Vector2 sep = AiNoSeparation ? Vector2.Zero : SeparationPush() * 2.2f;""")
rep("""            if (o == this || o.Dead) continue;""", """            if (o == this || o.Dead || o.AiNoSeparation) continue;""")
rep("""            case MonAI.Support:
            {""", """            case MonAI.Support when !Def.Is(MonTrait.Empower):
            {""")
rep("""    public void Busy(float time, Action end, Action<float> tick = null) { _st = Mode.Busy; _t = time; _busyEnd = end; _busyTick = tick; SetPhysicsProcess(tick != null); }""",
    """    public void Busy(float time, Action end, Action<float> tick = null) { _st = Mode.Busy; _t = time; _busyEnd = end; _busyTick = tick; _busyCancel = null; SetPhysicsProcess(tick != null); }""")
rep("""                case MonAI.Exploder: _t = 0.95f; _telegraph = Fx.Telegraph(Pos2, 3.6f, _t, Palette.Danger); Model.Flash(new Color(0.8f, 1f, 0.3f), 1.5f); break;
            }
        Model.PlayAttack(anim, sp, _t, 0.1f);          // the swing meets the moment the blow resolves""", """                case MonAI.Exploder: _t = 0.95f; _telegraph = Fx.Telegraph(Pos2, ExplodeRadius, _t, Palette.Danger); Model.Flash(Def.Element == Element.Poison ? new Color(0.8f, 1f, 0.3f) : Palette.Element(Def.Element), 1.5f); break;
            }
        if (Faction == Faction.Monster) anim = AiStartAttack(anim);
        Model.PlayAttack(anim, sp, _t, 0.1f);          // the swing meets the moment the blow resolves""")
rep("""        Vector3 hand = Model.BonePos("hand_r");
        if (Faction == Faction.Player) { MinionAttack(hand); return; }
        switch (Def.AI)
        {
            case MonAI.Ranged: case MonAI.Caster: case MonAI.Teleporter: case MonAI.Support:
            {
                Vector2 dir = (_target.Pos2 - Pos2).Normalized();
                hand.Y = Mathf.Clamp(hand.Y, 0.9f, 1.8f);
                Projectile.SpawnMonster(this, hand, dir, Def.ProjSpeed, Def.Range + 8f, 1f, Def.Element, Def.OnHit);
                Snd.At(Def.Sfx + "_attack", GlobalPosition, 0.7f);
                break;
            }
            case MonAI.Brute:
            {
                World.InCone(Faction.Monster, Pos2, _attackDir, Def.Range + Radius + 0.6f, 110f, Tmp);
                foreach (var a in Tmp.ToArray()) Combat.MonsterHit(this, a, 1f, Def.Element, Def.OnHit, true);
                Fx.Burst(World.To3(Pos2 + _attackDir * (Def.Range * 0.6f), 0.2f), Def.Element, 2.2f, 0.6f);
                Fx.Shake(0.2f, 0.2f);
                Snd.At(Def.Sfx + "_attack", GlobalPosition);
                break;
            }
            case MonAI.Exploder:
                Combat.MonsterArea(this, Pos2, 3.6f, 1f, Def.Element, St.Poison(0.5f, 4));
                Fx.Burst(Chest, Element.Poison, 3.6f, 1.2f);
                Snd.At("bloat_explode", GlobalPosition);
                NoLoot = false;
                TakeHit(new Hit { Source = this, Amount = MaxLife * 10f, NoReact = true });
                break;
            default:
            {
                float reach = Def.Range + Radius + _target.Radius + 0.5f;
                Vector2 to = _target.Pos2 - Pos2;
                if (to.Length() <= reach && to.Normalized().Dot(Forward) > 0.2f)
                {
                    Combat.MonsterHit(this, _target, 1f, Def.Element, Def.OnHit, true);""", """        Vector3 hand = Model.BonePos("hand_r");
        if (Faction == Faction.Player) { MinionAttack(hand); return; }
        if (AiResolveAttack(hand)) return;            // bombs, beams, the shield bash, eruptions … (MonsterAiKinds.cs)
        switch (Def.AI)
        {
            case MonAI.Ranged: case MonAI.Caster: case MonAI.Teleporter: case MonAI.Support: case MonAI.Caller when IsRanged:
            {
                Vector2 dir = (_target.Pos2 - Pos2).Normalized();
                hand.Y = Mathf.Clamp(hand.Y, 0.9f, 1.8f);
                AiShoot(hand, dir);
                Snd.At(Def.Sfx + "_attack", GlobalPosition, 0.7f);
                break;
            }
            case MonAI.Brute:
            {
                World.InCone(Faction.Monster, Pos2, _attackDir, Def.Range + Radius + 0.6f, 110f, Tmp);
                foreach (var a in Tmp.ToArray()) Strike(a, AiFirstMul());
                Fx.Burst(World.To3(Pos2 + _attackDir * (Def.Range * 0.6f), 0.2f), Def.Element, 2.2f, 0.6f);
                Fx.Shake(0.2f, 0.2f);
                Snd.At(Def.Sfx + "_attack", GlobalPosition);
                AiAfterSlam(Pos2 + _attackDir * (Def.Range * 0.6f));
                break;
            }
            case MonAI.Exploder:
            {
                // the bloated husk keeps its poison burst; the bestiary's exploders burst in their own element and status
                bool builtIn = !Def.OnHit.HasValue && Def.Element == Element.Poison;
                Combat.MonsterArea(this, Pos2, ExplodeRadius, AiFirstMul(), Def.Element, builtIn ? St.Poison(0.5f, 4) : Def.OnHit);
                if (!builtIn && Def.OnHit2.HasValue) Combat.MonsterArea(this, Pos2, ExplodeRadius, 0.01f, Def.Element, Def.OnHit2);
                Fx.Burst(Chest, Def.Element, ExplodeRadius, 1.2f);
                Snd.At(Def.Element == Element.Fire ? "fire_impact" : "bloat_explode", GlobalPosition);
                AiTrailAt(Pos2);
                NoLoot = false;
                TakeHit(new Hit { Source = this, Amount = MaxLife * 10f, NoReact = true });
                break;
            }
            default:
            {
                float reach = Def.Range + Radius + _target.Radius + 0.5f;
                Vector2 to = _target.Pos2 - Pos2;
                if (to.Length() <= reach && to.Normalized().Dot(Forward) > 0.2f)
                {
                    Strike(_target, AiFirstMul());""")
rep("""    public override float TakeHit(Hit h)
    {
        if (Dead) return 0;
        if (Faction == Faction.Monster) { h.Amount *= DrMul * (Staggered ? 1.25f : 1f); }
        float d = base.TakeHit(h);
        if (Dead) return d;""", """    public override float TakeHit(Hit h)
    {
        if (Dead) return 0;
        if (Faction == Faction.Monster) { h.Amount *= DrMul * (Staggered ? 1.25f : 1f); if (!AiModifyHit(ref h)) return 0f; }
        float d = base.TakeHit(h);
        if (Faction == Faction.Monster) AiAfterHit(h, d);
        if (Dead) return d;""")
rep("""            if (!h.NoReact && Brain == null && (_st == Mode.Chase || _st == Mode.Idle) && _flinchIcd <= 0 && h.Amount > MaxLife * 0.05f * Def.Poise && !IsElite)""",
    """            if (!h.NoReact && Brain == null && (_st == Mode.Chase || _st == Mode.Idle) && _flinchIcd <= 0 && h.Amount > MaxLife * 0.05f * Def.Poise && !IsElite && !AiNoFlinch)""")
rep("""    public override void Die(Hit h)
    {
        _st = Mode.Dead; _deadT = 0;
        CancelTelegraph();""", """    public override void Die(Hit h)
    {
        _st = Mode.Dead; _deadT = 0;
        CancelTelegraph();
        if (Faction == Faction.Monster) Log.Guard("monster-ai", () => AiOnDeath(h));""")
rep("""        if (Model.Dissolved || _deadT > 6f) QueueFree();""", """        if ((Model.Dissolved || _deadT > 6f) && !AiKeepsBody) QueueFree();""")
open(p, 'w', encoding='utf-8', newline='').write(s)
print("ok")
