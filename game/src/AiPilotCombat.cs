using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// AI pilot combat (only when <see cref="TestBot.Pilot"/> is set): reflexes that run every frame before the bot's
/// slower thinking. They read <see cref="DangerMap"/> — every hostile ground warning with its land time, every hostile
/// shot in flight and every hostile pool — simulate where the hero would be when each of them lands, and step to the
/// nearest safe ground. Walking comes first; the dodge roll (Space, i-frames 0.37 s) is used when walking cannot make it
/// in time, or to roll through a hit that is about to land; during a boss fight one roll is kept for the boss's own
/// marks. Positioning: ranged heroes keep their distance and kite, melee heroes close in on paths that avoid marks.
/// Potions at a threshold that wastes none, defensive skills when a hit cannot be avoided, a retreat when swarmed.
/// Everything goes through the player's own inputs (movement orders, keys, skill casts) — no cheats.
/// </summary>
public partial class TestBot
{
    private Vector2 _dgTo;
    private bool _dgActive;
    private double _dgIssued = -10, _dgSince, _pPotion = -10, _pRoll = -10, _pDef = -10, _kiteUntil = -10, _kiteNext = -10, _retreatUntil = -10, _retreatAt = -10, _dgLog = -10;
    private int _dgWalks, _dgRolls, _dgDefs;
    private readonly List<(Vector2 pos, float risk, float first, float score, float delay)> _cand = new(160);
    private static readonly float[] Delays = { 0.25f, 0.5f, 0.8f, 1.1f };

    private const float Horizon = 1.7f;
    private static bool _packMode;          // three or more enemies close: plain monsters' swings are not dodged (DangerMap.Danger.Minor)
    private int _castsSeen, _swampCasts = -1; private double _castSeenAt, _swampAt = -100;
    private const float RollLen = 5.5f, RollTime = 0.32f, RollGuard = 0.36f;

    /// <summary>Statistics for the pilot harness.</summary>
    internal string ReflexStats() => $"dodgeWalks={_dgWalks} rolls={_dgRolls} defensives={_dgDefs}";

    private static float HeroSpeed(Player p)
    {
        if (p.Rooted) return 0f;
        float v = Player.BaseSpeed * p.Hero.MoveMul * p.SlowMul;
        return Mathf.Max(1.5f, v);
    }

    // ───────────────────────── the risk of a path ─────────────────────────

    /// <summary>
    /// Weighted hits the hero would take on this plan over the next <paramref name="horizon"/> seconds: walking straight
    /// from <paramref name="from"/> to <paramref name="to"/> at <paramref name="speed"/> (or rolling there, protected for
    /// the first 0.36 s), then standing. Boss marks weigh 3, other marks 1, a shot 1.2, a pool 0.5.
    /// </summary>
    private static float Risk(Player p, Vector2 from, Vector2 to, float speed, bool roll, float horizon, out float firstHit, float delay = 0f)
    {
        firstHit = 99f;
        double now = DangerMap.Now;
        float pad = p.Radius + 0.3f;
        float dist = from.DistanceTo(to);
        Vector2 dir = dist > 0.01f ? (to - from) / dist : Vector2.Zero;
        Vector2 At(float t)
        {
            if (roll) return t >= RollTime ? to : from.Lerp(to, t / RollTime);
            return t <= delay ? from : from + dir * Mathf.Min(speed * (t - delay), dist);
        }
        bool Guarded(float t) => roll && t < RollGuard;
        float risk = 0f;
        var live = DangerMap.Live;
        var root = World.Root;
        for (int i = 0; i < live.Count; i++)
        {
            var d = live[i];
            if (d.Root != root || d.Until < now || DangerMap.Cancelled(d, now)) continue;
            if (d.Minor && _packMode) continue;            // in a pack the plain swings are traded, not dodged
            float t0 = Mathf.Max(0f, (float)(d.Land - now)), t1 = Mathf.Max(t0, (float)(d.Until - now));
            if (t0 > horizon) continue;
            for (float t = t0; t <= t1 + 0.001f; t += 0.08f)
            {
                if (Guarded(t)) continue;
                if (!d.Contains(At(t), pad)) continue;
                risk += d.Boss ? 3f : 1f;
                firstHit = Mathf.Min(firstHit, t);
                break;
            }
        }
        // shots in flight: straight lines (homing ones bend, which the next frame's look corrects)
        var shots = Projectile.Live;
        for (int i = 0; i < shots.Count; i++)
        {
            var s = shots[i];
            if (s == null || !GodotObject.IsInstanceValid(s) || !s.HostileShot || s.Speed <= 0.01f) continue;
            Vector2 q0 = s.Ground, hd = s.Heading;
            if (q0.DistanceSquaredTo(from) > 26f * 26f) continue;
            float life = Mathf.Min(horizon, s.RangeLeft / s.Speed);
            float hitR = s.HitRadius + p.Radius + 0.25f;
            for (float t = 0f; t <= life; t += 0.04f)
            {
                if (Guarded(t)) continue;
                Vector2 q = q0 + hd * (s.Speed * t);
                if (q.DistanceSquaredTo(At(t)) > hitR * hitR) continue;
                risk += 1.2f;
                firstHit = Mathf.Min(firstHit, t);
                break;
            }
        }
        // pools on the ground: standing in one at the end of the plan
        var zones = GroundZone.Live;
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z == null || !GodotObject.IsInstanceValid(z) || z.Done || !z.Hostile) continue;
            float r = z.Radius + p.Radius * 0.5f;
            Vector2 c = z.Center;
            Vector2 end = At(horizon), mid = At(horizon * 0.4f);
            if (end.DistanceSquaredTo(c) < r * r || mid.DistanceSquaredTo(c) < r * r) { risk += 0.5f; firstHit = Mathf.Min(firstHit, 0.5f); }
        }
        return risk;
    }

    /// <summary>How close the nearest upcoming mark comes to a spot (a little room to spare is better).</summary>
    private static float Crowding(Vector2 c, float pad)
    {
        double now = DangerMap.Now;
        float cost = 0f;
        var live = DangerMap.Live;
        for (int i = 0; i < live.Count; i++)
        {
            var d = live[i];
            if (d.Until < now || d.Land - now > 2.5) continue;
            if (d.Contains(c, pad + 1.3f)) cost += 1.2f;
        }
        var zones = GroundZone.Live;
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z != null && GodotObject.IsInstanceValid(z) && !z.Done && z.Hostile && z.Center.DistanceTo(c) < z.Radius + 1.5f) cost += 1f;
        }
        return cost;
    }

    // ───────────────────────── every frame ─────────────────────────

    /// <summary>
    /// The pilot's reflexes. Returns true when it is steering the hero this frame (the bot's thinking then waits).
    /// </summary>
    private bool PilotReflex(Game g, Player p, float dt)
    {
        Vector2 me = p.Pos2;
        DangerMap.Tick(me, p.Radius + 0.1f);
        _packMode = !PilotRanged(p.Hero) && CloseMonsters(me, 5f) >= 3 && p.LifeFrac > 0.4f;      // a ranged hero should not be in the pack at all: it keeps dodging
        // dodge-starved (astromancer campaign 2026-11-23: in a 30-monster lair horde it dodge-walked for 5+ s at a time with
        // 1–2 casts, the horde grew and it died 3 times): no cast for 1.5 s with 3+ close and over half life → the plain
        // swings are traded for ranged heroes too until it casts again (boss fights keep their own cast gate)
        if (Casts != _castsSeen) { _castsSeen = Casts; _castSeenAt = _clock; }
        if (!_packMode && _clock - _castSeenAt > 1.5 && p.LifeFrac > 0.5f && !DangerMap.BossFight() && CloseMonsters(me, 8f) >= 3) _packMode = true;
        PilotPotion(p);
        if (PilotCcShield(p, me)) return true;
        if (PilotShedChill(p, me)) return true;
        if (p.Stunned) return true;

        float v = HeroSpeed(p);
        float stay = Risk(p, me, me, 0f, false, Horizon, out float firstStay);

        if (_dgActive)
        {
            if (me.DistanceTo(_dgTo) < 0.45f || _clock - _dgSince > 2.5) _dgActive = false;
            else
            {
                float go = Risk(p, me, _dgTo, v, false, Horizon, out _);
                if (go <= 0.01f)
                {
                    if (_clock - _dgIssued > 0.3) { _dgIssued = _clock; p.SetMove(_dgTo, false); }
                    _doing = "dodge";
                    return true;
                }
            }
        }
        if (stay <= 0.01f) return _dgActive;

        // something is about to land here: find the nearest safe ground
        StopChannel();
        var grid = World.Grid;
        var target = PickedTarget();
        float ideal = PilotIdeal(p.Hero, target);
        _cand.Clear();
        float bestScore = float.MaxValue; int best = -1;
        void Consider(Vector2 c, float extra)
        {
            if (grid != null && (!grid.WalkAt(c) || !grid.LineWalkWide(me, c, p.Radius * 0.8f))) return;
            float risk = Risk(p, me, c, v, false, Horizon, out float first);
            float rad = me.DistanceTo(c);
            float score = risk * 60f + rad * 0.6f + Crowding(c, p.Radius) * 1.5f + extra + ArenaCost(me, c);
            if (target != null && IsInstanceValid(target)) score += Mathf.Abs(c.DistanceTo(target.Pos2) - ideal) * 0.25f;
            score += CloseMonsters(c, 2.6f) * 0.8f;
            _cand.Add((c, risk, first, score, 0f));
            if (score < bestScore) { bestScore = score; best = _cand.Count - 1; }
        }
        for (int ri = 0; ri < 8; ri++)
        {
            float rad = ri switch { 0 => 1.2f, 1 => 2.1f, 2 => 3.1f, 3 => 4.2f, 4 => 5.6f, 5 => 7.2f, 6 => 9.2f, _ => 11.5f };
            int n = ri < 2 ? 12 : 16;
            float off = ri * 0.37f;
            for (int k = 0; k < n; k++)
            {
                float a = off + k * Mathf.Tau / n;
                Consider(me + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad, 0f);
            }
        }
        // the safe hole of a ring about to land (a donut round the boss): its middle and its near side
        var live = DangerMap.Live;
        for (int i = 0; i < live.Count; i++)
        {
            var d = live[i];
            if (d.Kind != DangerKind.Ring || d.Inner < 1.5f || d.Land - DangerMap.Now > Horizon || d.Land < DangerMap.Now) continue;
            Vector2 toMe = (me - d.C).LengthSquared() > 0.01f ? (me - d.C).Normalized() : Vector2.Right;
            Consider(d.C + toMe * (d.Inner * 0.45f), 0f);
            Consider(d.C + toMe.Rotated(0.8f) * (d.Inner * 0.4f), 0f);
            Consider(d.C + toMe.Rotated(-0.8f) * (d.Inner * 0.4f), 0f);
        }
        // nothing clean at once: wait for a mark to land, then go (rings closing in, a second wave)
        if (best >= 0 && _cand[best].Item2 > 0.01f)
        {
            int n0 = _cand.Count;
            var order = new List<int>(n0);
            for (int i = 0; i < n0; i++) order.Add(i);
            order.Sort((x, y) => _cand[x].Item4.CompareTo(_cand[y].Item4));
            for (int oi = 0; oi < Math.Min(14, order.Count); oi++)
            {
                var cc = _cand[order[oi]];
                foreach (float delay in Delays)
                {
                    float risk = Risk(p, me, cc.Item1, v, false, Horizon, out float first, delay);
                    if (risk > 0.01f) continue;
                    float score = cc.Item4 - cc.Item2 * 60f + delay * 2f;
                    _cand.Add((cc.Item1, risk, first, score, delay));
                    if (score < bestScore || _cand[best].Item2 > 0.01f) { bestScore = score; best = _cand.Count - 1; }
                    break;
                }
            }
        }

        bool bossFight = DangerMap.BossFight();
        bool rollReady = p.EvadeCd <= 0f && !p.Rooted;
        var bw = best >= 0 ? _cand[best] : (me, stay, firstStay, float.MaxValue, 0f);
        if (bw.Item2 <= 0.01f && !(p.Busy && firstStay < 0.6f))
        {
            if (bw.Item5 > 0f)
            {
                // hold still: the way opens when the nearer marks have landed
                if (!p.Busy && _moving) { p.Stop(); _moving = false; }
                _dgActive = false;
                _doing = "dodge (wait)";
                return true;
            }
            WalkTo(p, bw.Item1, stay, firstStay);
            return true;
        }

        // walking cannot clear it in time: roll — out of it, or through it with the i-frames as it lands
        // the roll is kept for what matters: a boss mark, a hit at low life, or the chill (frost packs freeze a hero who has none)
        bool chilly = p.Has(StatusId.Chill) || CloseMonsters(me, 6f) >= 3 && FrostNear(me);
        bool bigHit = stay >= 3f || p.LifeFrac < 0.5f || !bossFight && (stay >= 2f || !chilly);
        if (rollReady && bigHit)
        {
            Vector2 rollEnd = me; float rollRisk = float.MaxValue; float rollScore = float.MaxValue;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.Pi / 4f;
                var dir = new Vector2(Mathf.Round(Mathf.Cos(a)), Mathf.Round(Mathf.Sin(a))).Normalized();
                Vector2 end = grid != null ? grid.ClampLine(me, me + dir * RollLen, false) : me + dir * RollLen;
                float r = Risk(p, me, end, 0f, true, Horizon, out _);
                float sc = r * 60f + Crowding(end, p.Radius) * 1.5f - end.DistanceTo(me) * 0.2f + (best >= 0 ? end.DistanceTo(bw.Item1) * 0.15f : 0f);
                if (sc < rollScore) { rollScore = sc; rollRisk = r; rollEnd = end; }
            }
            bool clean = rollRisk <= 0.01f;
            bool now = clean ? (firstStay < 0.9f || bw.Item2 > 0.01f) : firstStay < RollGuard - 0.06f;
            if (now && rollRisk < stay)
            {
                Roll(p, me, rollEnd, stay, firstStay, clean);
                return true;
            }
        }
        // a blink on the bar (astromancer, pactbinder): out of it in a tenth of a second
        if (firstStay > 0.12f && PilotBlink(p, me, stay)) return true;
        // a defensive skill when the hit cannot be avoided
        if (firstStay < 0.9f && (stay >= 3f || p.LifeFrac < 0.6f)) PilotDefensive(p, "unavoidable");
        // the least bad ground, unless the hero is better off holding still for the roll
        if (best >= 0 && bw.Item2 < stay && !p.Busy) { WalkTo(p, bw.Item1, stay, firstStay); return true; }
        return p.Busy || _dgActive;
    }

    private void WalkTo(Player p, Vector2 to, float risk, float first)
    {
        bool fresh = !_dgActive || to.DistanceTo(_dgTo) > 0.8f;
        _dgTo = to; _dgActive = true;
        if (fresh) { _dgSince = _clock; _dgWalks++; }
        if (fresh || _clock - _dgIssued > 0.3)
        {
            _dgIssued = _clock;
            p.SetMove(to, false);
            _moving = true;
        }
        _doing = "dodge";
        if (fresh && _clock - _dgLog > 2.0)
        {
            _dgLog = _clock;
            Log.Debug("pilot", $"dodge: walks {p.Pos2.DistanceTo(to):0.0} m (risk {risk:0.#}, lands in {first:0.00}s)");
        }
    }

    /// <summary>The dodge roll: the movement keys pick the direction (W = −Z, D = +X), Space rolls.</summary>
    private void Roll(Player p, Vector2 me, Vector2 end, float risk, float first, bool clean)
    {
        Vector2 dir = end - me;
        if (dir.LengthSquared() < 0.01f) dir = -p.Forward;
        dir = dir.Normalized();
        GetViewport()?.GuiReleaseFocus();
        p.Stop();
        if (dir.X > 0.38f) Hold(Key.D, 0.15f); else if (dir.X < -0.38f) Hold(Key.A, 0.15f);
        if (dir.Y > 0.38f) Hold(Key.S, 0.15f); else if (dir.Y < -0.38f) Hold(Key.W, 0.15f);
        Hold(Key.Space, 0.08f);
        _pRoll = _clock; _lastEvade = _clock;
        _dgRolls++;
        _dgActive = false;
        _doing = "roll";
        Log.Info("pilot", $"dodge: rolls {(clean ? "clear" : "through")} (risk {risk:0.#}, lands in {first:0.00}s, life {p.LifeFrac * 100f:0}%)");
    }


    /// <summary>Weighted count of monsters on the hero near a spot (elite / champion 2, boss 3): how hard a crowd hits.</summary>
    private static int Pressure(Vector2 c, float r)
    {
        int n = 0;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || !m.Aggro || !IsInstanceValid(m)) continue;
            if (m.Pos2.DistanceSquaredTo(c) >= (r + m.Radius) * (r + m.Radius)) continue;
            n += m.IsBoss ? 3 : m.IsElite ? 2 : 1;
        }
        return n;
    }
    private static int CloseMonsters(Vector2 c, float r)
    {
        int n = 0;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || !m.Aggro || !IsInstanceValid(m)) continue;
            if (m.Pos2.DistanceSquaredTo(c) < (r + m.Radius) * (r + m.Radius)) n++;
        }
        return n;
    }

    // ───────────────────────── potions and defensive skills ─────────────────────────

    /// <summary>
    /// Potions (one per 30 s comes back, all of them in town): with a full belt the regeneration is wasted, so drink at
    /// 70 %; otherwise at 50 % (55 % against a boss), or at 65 % when a big mark is about to land and cannot be avoided.
    /// </summary>
    private void PilotPotion(Player p)
    {
        if (p.Dead) return;
        float life = p.LifeFrac;
        float in2 = DangerMap.Intake(2.0) / Mathf.Max(1f, p.MaxLife);       // share of the life bar lost in the last 2 s
        if (p.Potions > 0 && _clock - _pPotion >= 1.0)
        {
            bool full = p.Potions >= p.Hero.D.PotionCap;
            bool boss = DangerMap.BossFight();
            float th = full ? 0.7f : boss ? 0.55f : 0.5f;
            bool spike = life < 0.8f && life - in2 < 0.3f;                 // two more seconds like the last two would leave < 30 %
            if (life < th || spike)
            {
                _pPotion = _clock; _lastPotion = _clock;
                Tap(Key.Q);
                Log.Info("pilot", $"potion at {life * 100f:0}% (lost {in2 * 100f:0}% in 2 s; {p.Potions - 1} left)");
            }
        }
        if (life < 0.45f && in2 > 0.2f) PilotDefensive(p, "damage spike");
    }

    /// <summary>A defensive buff on the bar (barrier, armour, immunity) when danger spikes.</summary>
    private bool PilotDefensive(Player p, string why)
    {
        if (_clock - _pDef < 1.0 || p.Busy) return false;
        var hero = p.Hero;
        for (int slot = 0; slot < 6; slot++)
        {
            string id = hero.D.Bar[slot];
            if (id == null) continue;
            var d = hero.Eff(id);
            if (d == null || d.Cat != SkillCat.Defensive || hero.Rank(id) <= 0 || p.WhyCant(d) != null) continue;
            if (d.Kind != SkillKind.Buff && CloseMonsters(p.Pos2, 4f) == 0 && !DangerMap.BossFight()) continue;      // a nova or a smoke bomb wants enemies close
            if (d.Kind == SkillKind.Buff && d.Duration > 0f && HasBuff(hero, id + "_t")) continue;
            if (!Cast(p, slot, p.Pos2 + p.Forward * 2f, null)) continue;
            _pDef = _clock; _dgDefs++;
            Log.Info("pilot", $"defensive {id} ({why})");
            return true;
        }
        return false;
    }

    // ───────────────────────── positioning ─────────────────────────

    /// <summary>The distance the hero wants from this target: in reach for melee; for ranged, out of a boss's swing.</summary>
    private float PilotIdeal(Hero hero, Monster t)
    {
        var basic = hero.Eff(hero.D.Bar[0]) ?? hero.Eff(hero.D.Bar[1]);
        float want = basic == null || t == null ? 2.5f : Reach(basic, t);
        if (want < 6f) return Mathf.Max(1.2f, want * 0.85f);
        float ideal = Mathf.Clamp(want * 0.85f, 6f, 11f);
        if (t != null && t.Brain != null) ideal = Mathf.Max(ideal, Mathf.Min(want - 0.5f, 9f));
        return ideal;
    }

    /// <summary>
    /// Pilot fight movement (instead of walking straight at the target): ranged heroes kite back to their distance when
    /// something reaches them; everybody approaches along a line that does not cross a mark about to land.
    /// Returns true when it gave a movement order (skills wait).
    /// </summary>
    private bool PilotPosition(Player p, Hero hero, Monster t, Vector2 me, float dist, bool los, float want, bool beforeSkills)
    {
        Vector2 tp = t.Pos2;
        bool ranged = want >= 6f;
        float ideal = PilotIdeal(hero, t);
        float v = HeroSpeed(p);
        if (beforeSkills)
        {
            if (!ranged || _clock < _kiteNext) return _clock < _kiteUntil;
            int close = CloseMonsters(me, 3f);
            bool boss = t.Brain != null;
            float in2 = DangerMap.Intake(2.0) / Mathf.Max(1f, p.MaxLife);
            bool kite = close > 0 && (boss || t.IsElite || p.LifeFrac < 0.75f || close >= 3 || in2 > 0.12f) || boss && dist < ideal - 2.5f;
            if (!kite) return false;
            Vector2? spot = RingSpot(p, me, tp, ideal, v, true);
            if (spot == null) return false;
            _kiteUntil = _clock + 0.75; _kiteNext = _clock + (in2 > 0.12f ? 1.2 : 1.9);
            MoveTo(p, spot.Value, 0.0);
            _doing = "kite";
            return true;
        }
        if (dist <= want && los) { if (_moving) { p.Stop(); _moving = false; } return false; }
        // approach: straight if that path is clear of marks, else a spot at the wanted distance that is
        Vector2 goal = ranged ? tp + (me - tp).Normalized() * Mathf.Min(ideal, dist) : tp;
        if (Risk(p, me, me + (goal - me).LimitLength(v * Horizon), v, false, Horizon, out _) <= 0.01f) { MoveTo(p, goal, 0.35); return true; }
        Vector2? alt = RingSpot(p, me, tp, ranged ? ideal : Mathf.Max(want * 0.8f, 1.2f), v, ranged);
        if (alt != null) { MoveTo(p, alt.Value, 0.35); return true; }
        if (_moving) { p.Stop(); _moving = false; }
        return true;      // wait for the marks to land
    }

    /// <summary>A walkable spot on the circle of radius <paramref name="r"/> round <paramref name="c"/>, safe to walk to, near the hero.</summary>
    private Vector2? RingSpot(Player p, Vector2 me, Vector2 c, float r, float v, bool needLos)
    {
        var grid = World.Grid;
        Vector2? best = null; float bs = float.MaxValue;
        Vector2 from = (me - c).LengthSquared() > 0.01f ? (me - c).Normalized() : Vector2.Right;
        for (int k = 0; k < 20; k++)
        {
            float a = (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * Mathf.Tau / 20f;
            Vector2 s = c + from.Rotated(a) * r;
            if (grid != null && (!grid.WalkAt(s) || needLos && !grid.LineShot(s, c))) continue;
            float d = me.DistanceTo(s);
            if (grid != null && d < 12f && !grid.LineWalkWide(me, s, p.Radius * 0.8f)) continue;
            float risk = Risk(p, me, s, v, false, Horizon, out _);
            float sc = risk * 50f + d + CloseMonsters(s, 3f) * 3f + Crowding(s, p.Radius) * 2f + ArenaCost(me, s) + SleepersNear(s, 14f) * 6f;      // not kiting into the next pack's notice (13.5 m)
            if (sc < bs) { bs = sc; best = s; }
        }
        return best;
    }

    /// <summary>
    /// Swarmed and losing fast (or low with no potion left): back off, away from the crowd, rolling when it is bad, until
    /// the potions and the rest of the reflexes have caught up. In a boss fight the step back is shorter (the arena).
    /// </summary>
    private bool PilotRetreat(Player p, Vector2 me)
    {
        SafeTrail(me);
        if (_clock < _retreatUntil) { _doing = "retreat"; return true; }
        if (_clock - _retreatAt < 3.0) return false;          // a breather between retreats: fight back in between
        float in2 = DangerMap.Intake(2.0) / Mathf.Max(1f, p.MaxLife);
        float life = p.LifeFrac;
        int packed = CloseMonsters(me, 3.5f);
        bool bad = life < 0.4f && (p.Potions == 0 || in2 >= 0.2f) || life < 0.6f && in2 >= 0.3f
            || packed >= 4 && in2 >= 0.2f && life < 0.6f;             // surrounded and bleeding: string the pack out
        // swamped before the life bar shows it (sh_frost with the bestiary roster, lv24 hunter: 15+ attackers took the
        // full bar in ~2 s, too fast for the reactive rule): a big crowd on the hero that already bites → back off early
        var hero0 = Game.I?.Hero;
        bool ranged0 = hero0 != null && PilotRanged(hero0);
        int press = Pressure(me, 7f);
        bool swamped = DangerMap.BossFight() && press >= (ranged0 ? 6 : 10) && (in2 >= 0.12f || life < 0.8f);      // boss fights only: in the open world running pulls the next packs (lv1 pactbinder, wastes)
        // ...but not as a habit: an early back-off is followed by fighting (a cast since the last one, 6 s apart) — a lv1
        // pactbinder (final round) backed off every 3 s, killed nothing for 50 s and dragged a 6-monster crowd up to 45
        if (swamped && !bad && (_clock - _swampAt < 6.0 || Casts == _swampCasts)) swamped = false;
        if (!bad && !swamped) return false;
        bool boss = DangerMap.BossFight();
        Vector2 sum = Vector2.Zero; int n = 0;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || !m.Aggro || !IsInstanceValid(m) || m.Pos2.DistanceTo(me) > 8f) continue;
            sum += m.Pos2; n++;
        }
        if (n < (boss ? 3 : 2)) return false;
        Vector2 away = (me - sum / n);
        away = away.LengthSquared() < 0.01f ? -p.Forward : away.Normalized();
        var grid = World.Grid;
        Vector2? safe = SafeSpot(me);
        Vector2 best = me; float bs = float.MinValue;
        for (int k = 0; k < 12; k++)
        {
            Vector2 dir = away.Rotated((k - 6) * Mathf.Pi / 8f);
            float reach = boss ? 7f : 12f;
            Vector2 end = grid != null ? grid.ClampLine(me, me + dir * reach, false) : me + dir * reach;
            float sc = end.DistanceTo(me) + dir.Dot(away) * 6f - CloseMonsters(end, 4f) * 5f - ArenaCost(me, end) - SleepersNear(end, 9f) * 4f
                + (safe != null ? dir.Dot((safe.Value - me).Normalized()) * 8f : 0f);      // not into the next pack; back the way it came (cleared ground)
            if (sc > bs) { bs = sc; best = end; }
        }
        if (best.DistanceTo(me) < 3f) return false;
        StopChannel();
        MoveTo(p, best, 0.0);
        if (p.EvadeCd <= 0f && (n >= 4 || in2 > 0.3f)) EvadeFrom(p, grid, me, away);
        _retreatUntil = _clock + (boss ? 1.0 : 1.6); _retreatAt = _clock;
        _doing = "retreat";
        if (!bad) { _swampAt = _clock; _swampCasts = Casts; }
        Log.Info("pilot", $"swarmed by {n} at {p.LifeFrac * 100f:0}% life (potions {p.Potions}, lost {in2 * 100f:0}% in 2 s): backs off{(bad ? "" : $" early (pressure {press})")}");
        return true;
    }
}

public partial class TestBot
{
    /// <summary>
    /// A boss-brained monster that is not this place's quest boss and has not noticed the hero (a world boss, a named
    /// rare on its patrol): the pilot does not start that fight. One that attacks is fought (or the hero backs away).
    /// </summary>
    private static bool PilotLeaveAlone(Monster m, Level level)
    {
        if (m.Aggro || level == null) return false;
        // a hero of level 1–4 does not wake an elite pack (lv1 pactbinder, final round: an elite dusk-hound pack in the
        // wastes killed it in each of three runs); it comes back for them a few levels later
        var h = Game.I?.Hero;
        bool hold = false; foreach (var s in level.StrongholdMobs) if (s == m) { hold = true; break; }
        if (h != null && h.D.Level < 5 && m != level.BossMon && !hold && ElitePack(m)) return true;
        // one pack at a time below lv5: no sleeping monster is picked while an awake one is within 20 m (pactbinder lv1,
        // EARLY GENTLE build: hounds, husks, cutters and grubs of several packs at once → death at 106 s)
        if (h != null && h.D.Level < 5 && m != level.BossMon && !hold && World.P != null && CloseMonsters(World.P.Pos2, 20f) > 0) return true;
        if (m.Brain == null) return false;
        if (m == level.BossMon) return false;
        foreach (var s in level.StrongholdMobs) if (s == m) return false;
        return true;
    }

    private static bool ElitePack(Monster m)
    {
        if (m.IsElite) return true;
        if (m.Pack < 0) return false;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++) { var o = list[i]; if (o != null && !o.Dead && o.Pack == m.Pack && o.IsElite) return true; }
        return false;
    }
}

public partial class TestBot
{
    private double _lastCastOk = -10;

    /// <summary>
    /// During a boss fight: a skill whose swing would still lock the hero when the boss's next attack could land is held
    /// back (the boss needs at least ~0.6 s of wind-up, the hero ~0.45 s to step out). Defensive skills always go; and
    /// after 2.5 s without a cast the hero attacks anyway (the boss would otherwise never be hit).
    /// </summary>
    private bool PilotMayCast(Player p, Hero hero, SkillDef d)
    {
        if (d.Cat == SkillCat.Defensive || d.Kind == SkillKind.Buff) return true;
        var boss = NearBoss(p.Pos2, 16f);
        if (boss == null) { _lastCastOk = _clock; return true; }
        var b = boss.Brain;
        float lockT = d.Kind == SkillKind.Channel ? 0.3f : d.Lock / Mathf.Max(0.3f, hero.AttackSpeedMul);
        float dist = boss.Pos2.DistanceTo(p.Pos2);
        float next = b.SpecialIn;
        if (dist <= b.BasicReach + 1.5f) next = Mathf.Min(next, b.BasicIn);
        float window = next + 0.6f - 0.45f;
        if (lockT <= window || _clock - _lastCastOk > 2.5) { _lastCastOk = _clock; return true; }
        return false;
    }

    private static Monster NearBoss(Vector2 me, float r)
    {
        Monster best = null; float bd = r * r;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || m.Brain == null || !m.Aggro || !IsInstanceValid(m)) continue;
            float d = m.Pos2.DistanceSquaredTo(me);
            if (d < bd) { bd = d; best = m; }
        }
        return best;
    }
}

public partial class TestBot
{
    /// <summary>
    /// Chill stacks up to a 2 s freeze (Actor.ApplyStatus) — frozen, the hero can neither move, roll nor drink. The dodge
    /// roll clears crowd control (Player.TryEvade → ClearCc), so with the chill past 60 % the pilot rolls it off, away
    /// from the enemies, before it turns into ice.
    /// </summary>
    private bool PilotShedChill(Player p, Vector2 me)
    {
        if (p.Stunned || p.Rooted || p.EvadeCd > 0f || _clock - _pRoll < 0.5) return false;
        float chill = p.Has(StatusId.Chill) ? p.StMag[(int)StatusId.Chill] : 0f;
        int near = CloseMonsters(me, 3.5f);
        // in a frost crowd a few blows fill the bar at once (wildsoul sh_frost lv28: 10 freezes, all "chill full", 7 by
        // frostbound, mostly from under 50 %): shed it early when more are on the hero
        if (chill < (near >= 5 ? 0.25f : near >= 3 ? 0.35f : 0.6f)) return false;
        var grid = World.Grid;
        Vector2 end = me; float bs = float.MaxValue;
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.Pi / 4f;
            var dir = new Vector2(Mathf.Round(Mathf.Cos(a)), Mathf.Round(Mathf.Sin(a))).Normalized();
            Vector2 e = grid != null ? grid.ClampLine(me, me + dir * RollLen, false) : me + dir * RollLen;
            float sc = Risk(p, me, e, 0f, true, Horizon, out _) * 60f + CloseMonsters(e, 3.5f) * 4f + Crowding(e, p.Radius) * 1.5f - e.DistanceTo(me);
            if (sc < bs) { bs = sc; end = e; }
        }
        Log.Info("pilot", $"chill {chill * 100f:0}%: rolls it off before it freezes");
        Roll(p, me, end, 0f, 99f, true);
        return true;
    }
}

public partial class TestBot
{
    /// <summary>A blink skill to the safest spot in its range when neither walking nor the roll gets the hero clear.</summary>
    private bool PilotBlink(Player p, Vector2 me, float stay)
    {
        if (p.Busy || p.Rooted) return false;
        var hero = p.Hero;
        var grid = World.Grid;
        for (int slot = 0; slot < 6; slot++)
        {
            string id = hero.D.Bar[slot];
            if (id == null) continue;
            var d = hero.Eff(id);
            if (d == null || d.Kind != SkillKind.Blink || hero.Rank(id) <= 0 || p.WhyCant(d) != null) continue;
            float range = Mathf.Max(4f, d.Range * 0.9f);
            Vector2 best = me; float bs = float.MaxValue, br = stay;
            for (int ri = 1; ri <= 3; ri++)
            {
                float rad = range * ri / 3f;
                for (int k = 0; k < 16; k++)
                {
                    Vector2 c = me + Vector2.Right.Rotated(k * Mathf.Tau / 16f + ri * 0.2f) * rad;
                    if (grid != null && (!grid.WalkAt(c) || !grid.LineShot(me, c))) continue;
                    float r = Risk(p, c, c, 0f, false, Horizon, out _);
                    float sc = r * 60f + CloseMonsters(c, 3.5f) * 3f + Crowding(c, p.Radius) * 1.5f + rad * 0.1f;
                    if (sc < bs) { bs = sc; best = c; br = r; }
                }
            }
            if (br >= stay || best == me) return false;
            if (!Cast(p, slot, best, null)) return false;
            _dgActive = false; _doing = "blink";
            Log.Info("pilot", $"dodge: blinks {me.DistanceTo(best):0.0} m with {id} (risk {stay:0.#} → {br:0.#})");
            return true;
        }
        return false;
    }
}

public partial class TestBot
{
    /// <summary>A frost monster close by (its blows chill).</summary>
    private static bool FrostNear(Vector2 me)
    {
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || !m.Aggro || !IsInstanceValid(m) || m.Def == null) continue;
            if (m.Def.Element == Element.Frost && m.Pos2.DistanceSquaredTo(me) < 64f) return true;
        }
        return false;
    }

    private double _respawnedAt = -10;

    /// <summary>
    /// Just back on its feet (the respawn grants a moment of protection): when the checkpoint sits next to the pack that
    /// killed the hero, walk away from it first instead of fighting it again at once.
    /// </summary>
    internal void PilotRespawned() { _respawnedAt = _clock; _lastRespawn = _clock; }
    private double _lastRespawn = -100;

    private bool PilotAfterRespawn(Player p, Vector2 me)
    {
        if (_clock - _respawnedAt > 6.0) return false;
        Vector2 sum = Vector2.Zero; int n = 0;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || !IsInstanceValid(m) || m.Pos2.DistanceTo(me) > 11f || m.Brain != null) continue;
            if (!m.Aggro && m.Pos2.DistanceTo(me) > 7f) continue;
            sum += m.Pos2; n++;
        }
        if (n < 3) { _respawnedAt = -10; return false; }
        Vector2 away = me - sum / n;
        away = away.LengthSquared() < 0.01f ? -p.Forward : away.Normalized();
        var grid = World.Grid;
        Vector2 best = me; float bs = float.MinValue;
        for (int k = 0; k < 12; k++)
        {
            Vector2 dir = away.Rotated((k - 6) * Mathf.Pi / 8f);
            Vector2 end = grid != null ? grid.ClampLine(me, me + dir * 14f, false) : me + dir * 14f;
            float sc = end.DistanceTo(me) + dir.Dot(away) * 8f - CloseMonsters(end, 5f) * 6f;
            if (sc > bs) { bs = sc; best = end; }
        }
        if (best.DistanceTo(me) < 3f) { _respawnedAt = -10; return false; }
        StopChannel();
        MoveTo(p, best, 0.4);
        _doing = "away from the pack";
        if (_clock - _dgLog > 2.0) { _dgLog = _clock; Log.Info("pilot", $"respawned next to {n} monsters: walks {me.DistanceTo(best):0} m away first"); }
        return true;
    }
}

public partial class TestBot
{
    private double _frostUntil = -10, _frostNext = -10, _frostLog = -10;

    /// <summary>
    /// Frost packs: every frost blow adds chill and the fourth one freezes for 2 s (then 1.5 s of immunity, and it starts
    /// again) — a hero standing inside a pack of frost melee spends half the fight frozen. So the pilot never lets one
    /// surround it: with three or more of them in reach (or two once chilled, or any just after a thaw) it steps back
    /// out of reach; the pack strings out (they are slower than the hero) and the first ones are fought one at a time.
    /// </summary>
    private bool PilotFrostDiscipline(Player p, Vector2 me)
    {
        if (_clock < _frostUntil) { _doing = "frost: step back"; return true; }
        if (_clock < _frostNext || p.Stunned || p.Rooted) return false;
        float chill = p.Has(StatusId.Chill) ? p.StMag[(int)StatusId.Chill] : 0f;
        Vector2 sum = Vector2.Zero; int frost = 0;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || !m.Aggro || m.Brain != null || !IsInstanceValid(m) || m.Def == null || m.Def.Element != Element.Frost) continue;
            if (m.Def.Speed > HeroSpeed(p) * 0.75f) continue;      // a swarmer faster than the hero: stepping back only gives it free blows
            if (m.Pos2.DistanceTo(me) > 3.6f + m.Radius) continue;
            sum += m.Pos2; frost++;
        }
        bool thawed = p.CcImmune > 0f && frost >= 1;
        if (frost >= 3) PilotDefensive(p, "frost pack closing in");      // a smoke bomb or frost nova stuns the pack before it freezes the hero
        // only when the next blows would freeze and the roll cannot clear it: stepping back costs the fight its damage
        bool rollSoon = p.EvadeCd <= 0.6f;
        bool ranged = PilotRanged(p.Hero);      // a ranged hero loses little by stepping back, and frost blows are what freeze it
        if (!(thawed && frost >= 2 || chill >= 0.5f && !rollSoon && frost >= 2 || ranged && frost >= 1)) return false;
        Vector2 away = me - sum / frost;
        away = away.LengthSquared() < 0.01f ? -p.Forward : away.Normalized();
        var grid = World.Grid;
        Vector2 best = me; float bs = float.MinValue;
        for (int k = 0; k < 12; k++)
        {
            Vector2 dir = away.Rotated((k - 6) * Mathf.Pi / 8f);
            Vector2 end = grid != null ? grid.ClampLine(me, me + dir * 6.5f, false) : me + dir * 6.5f;
            float sc = end.DistanceTo(me) + dir.Dot(away) * 5f - CloseMonsters(end, 3.5f) * 6f - Risk(p, me, end, HeroSpeed(p), false, 1.2f, out _) * 20f;
            if (sc > bs) { bs = sc; best = end; }
        }
        if (best.DistanceTo(me) < 2.5f) { _frostNext = _clock + 1.0; return false; }
        StopChannel();
        MoveTo(p, best, 0.0);
        _frostUntil = _clock + 0.75; _frostNext = _clock + (ranged ? 0.5 : 1.5);
        _doing = "frost: step back";
        if (_clock - _frostLog > 6.0) { _frostLog = _clock; Log.Info("pilot", $"frost pack ({frost} in reach, chill {chill * 100f:0}%{(thawed ? ", just thawed" : "")}): steps back to string it out"); }
        return true;
    }
}

public partial class TestBot
{
    private double _ccImmuneAt = -10;

    /// <summary>
    /// A defensive skill that makes the hero immune to crowd control (with the specialisation PreferUnstoppable picks) is
    /// cast before the freeze, not after: as soon as a frost pack closes in or a boss fight is on.
    /// </summary>
    private bool PilotCcShield(Player p, Vector2 me)
    {
        if (p.Busy || p.Stunned || _clock - _ccImmuneAt < 1.0) return false;
        if (p.Hero.BuffFlag(b => b.Unstoppable || b.Immune)) return false;
        int frost = 0;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || !m.Aggro || !IsInstanceValid(m) || m.Def == null || m.Def.Element != Element.Frost) continue;
            if (m.Pos2.DistanceSquaredTo(me) < 7f * 7f) frost++;
        }
        bool want = frost >= 2 || p.Has(StatusId.Chill) && frost >= 1 || DangerMap.BossFight() && CloseMonsters(me, 8f) >= 3;
        if (!want) return false;
        var hero = p.Hero;
        for (int slot = 0; slot < 6; slot++)
        {
            string id = hero.D.Bar[slot];
            if (id == null) continue;
            var d = hero.Eff(id);
            if (d == null || d.Cat != SkillCat.Defensive || hero.Rank(id) <= 0 || p.WhyCant(d) != null) continue;
            if (!d.Buffs.Exists(b => b.Unstoppable || b.Immune)) continue;
            if (!Cast(p, slot, me + p.Forward * 2f, null)) continue;
            _ccImmuneAt = _clock; _pDef = _clock; _dgDefs++;
            Log.Info("pilot", $"defensive {id} before the freeze (frost {frost}, chill {(p.Has(StatusId.Chill) ? p.StMag[(int)StatusId.Chill] * 100f : 0f):0}%)");
            return true;
        }
        _ccImmuneAt = _clock;
        return false;
    }
}

public partial class TestBot
{
    /// <summary>
    /// The boss fight's arena: a boss lets go and heals in full when the hero stays more than 30 m outside its arena
    /// (BossBrain leash), so during a boss fight every spot the pilot picks (dodges, kiting, retreats) pays for leaving
    /// it, adds far outside it are not chased, and a hero who drifted out walks back.
    /// </summary>
    private static Monster _arenaBoss;
    private static double _arenaAt = -10;

    private static Monster ArenaBoss(Vector2 me)
    {
        double now = DangerMap.Now;
        if (now - _arenaAt < 0.5 && (_arenaBoss == null || IsInstanceValid(_arenaBoss) && !_arenaBoss.Dead)) return _arenaBoss;
        _arenaAt = now;
        _arenaBoss = NearBoss(me, 45f);
        return _arenaBoss;
    }

    /// <summary>Cost of standing at <paramref name="c"/> for the boss leash (0 inside the arena and a margin).</summary>
    private static float ArenaCost(Vector2 me, Vector2 c)
    {
        var b = ArenaBoss(me);
        if (b == null || b.Brain == null) return 0f;
        float over = c.DistanceTo(b.Brain.Arena) - (b.Brain.ArenaR + 8f);
        return over > 0f ? over * 3f : 0f;
    }

    /// <summary>The target to fight during a boss fight: an add far outside the arena is left for the boss.</summary>
    private Monster PilotArenaTarget(Monster t, Vector2 me)
    {
        var b = ArenaBoss(me);
        if (b == null || b.Brain == null || t == null || t == b) return t;
        if (t.Pos2.DistanceTo(b.Brain.Arena) > b.Brain.ArenaR + 10f && t.Pos2.DistanceTo(me) > 7f) return b;      // an add on the hero is fought wherever it stands
        return t;
    }

    /// <summary>Drifted out of the boss arena (kiting, a retreat, a chase): walk back before the boss lets go.</summary>
    private bool PilotBackToArena(Player p, Vector2 me)
    {
        var b = ArenaBoss(me);
        if (b == null || b.Brain == null) return false;
        Vector2 a = b.Brain.Arena;
        float r = b.Brain.ArenaR;
        if (me.DistanceTo(a) < r + 18f) return false;
        // only when the fight is worth keeping and the walk back is safe: a boss still near full life may as well reset,
        // and walking a hurt hero (or one just back on its feet) into the crowd that killed it is how the leash killed hunters
        if (b.MaxLife > 0f && b.Life > b.MaxLife * 0.9f) return false;
        if (p.LifeFrac < 0.7f || p.Potions < 2 || _clock - _lastRespawn < 15.0 || CloseMonsters(me, 8f) >= 3) return false;
        Vector2 to = a + (me - a).LimitLength(r * 0.6f);
        MoveTo(p, to, 0.5);
        _doing = "back to the arena";
        if (_clock - _dgLog > 3.0) { _dgLog = _clock; Log.Info("pilot", $"{me.DistanceTo(a):0} m from {b.Def?.Id}'s arena: walks back before it lets go"); }
        return true;
    }
}

public partial class TestBot
{
    /// <summary>A hero whose basic attack reaches 6 m or more (hunter, casters): it fights from range and kites.</summary>
    private static bool PilotRanged(Hero hero)
    {
        var basic = hero.Eff(hero.D.Bar[0]) ?? hero.Eff(hero.D.Bar[1]);
        if (basic == null) return false;
        return basic.Kind switch
        {
            SkillKind.Melee or SkillKind.Nova or SkillKind.Channel or SkillKind.Dash or SkillKind.Leap => false,
            _ => basic.Range >= 6f,
        };
    }
}

public partial class TestBot
{
    private double _hintLog = -10;

    /// <summary>
    /// Named encounters say what a player is meant to do (NamedScript.BotTarget / BotStand / BotPassive — the hints
    /// the named test suite plays by): break the garrison banner before the frost commander's musters bury the hero
    /// (瓦雷克), topple the storm pylons first (第七號), stand where the script wants, leave a passive boss alone. The
    /// pilot follows them while such a fight is on near the hero. Returns the target to fight (or the one it had).
    /// </summary>
    private Monster PilotNamedHint(Player p, Monster t, Vector2 me, out Vector2? stand)
    {
        stand = null;
        var live = NamedHub.Live;
        for (int i = 0; i < live.Count; i++)
        {
            var e = live[i];
            if (e == null || !IsInstanceValid(e) || !e.Alive || e.S == null) continue;
            var boss = e.M;
            if (!boss.Aggro || boss.Pos2.DistanceTo(me) > 35f) continue;
            Monster hint = null; bool passive = false;
            try { hint = e.S.BotTarget; stand = e.S.BotStand; passive = e.S.BotPassive; } catch (Exception) { stand = null; continue; }
            if (hint != null && IsInstanceValid(hint) && !hint.Dead && !hint.Untargetable && hint.Pos2.DistanceTo(me) < 30f)
            {
                // an add on the hero still comes first (a frost blow freezes whatever the plan was)
                if (t != null && t != hint && t != boss && t.Pos2.DistanceTo(me) < 3f) return t;
                // the hint stands in a crowd (or one already bites): thin what is near first, then go for it — walking
                // into 15 attackers for the banner killed a lv24 hunter in ~2 s (bestiary roster, 2026-11-23)
                var hh = Game.I?.Hero;
                int lim = hh != null && PilotRanged(hh) ? 5 : 8;
                if (t != null && t != hint && hint.Pos2.DistanceTo(me) > 6f && (Pressure(hint.Pos2, 6f) >= lim + 2 || Pressure(me, 7f) >= lim))
                {
                    if (_clock - _hintLog > 8.0) { _hintLog = _clock; Log.Info("pilot", $"{boss.Def?.Id}: crowd at {hint.DisplayName ?? hint.Def?.Id}, thins it first"); }
                    return t;
                }
                if (_clock - _hintLog > 8.0) { _hintLog = _clock; Log.Info("pilot", $"{boss.Def?.Id}: goes for {hint.DisplayName ?? hint.Def?.Id} first (the encounter's mechanic)"); }
                return hint;
            }
            if (passive && t == boss) return null;
            return t;
        }
        return t;
    }
}

public partial class TestBot
{
    /// <summary>Monsters that have not noticed the hero yet near a spot (a retreat that runs into them pulls a second pack).</summary>
    private static int SleepersNear(Vector2 c, float r)
    {
        int n = 0;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || m.Aggro || !IsInstanceValid(m)) continue;
            if (m.Pos2.DistanceSquaredTo(c) < r * r) n++;
        }
        return n;
    }
}

public partial class TestBot
{
    private Level _yardLevel;
    private double _yardSince = -1, _yardLog = -10;

    /// <summary>
    /// Before a stronghold's commander is woken: clear the garrison round it pack by pack from outside its notice
    /// (monsters notice the hero at 13.5 m with a line of sight and wake their whole pack). The wildsoul's final-round
    /// campaign woke sh_frost with 20+ monsters of the yard on top (frostbound, reavers, yaks) and was stun-/freeze-locked
    /// to death 8 times between lv22 and lv32. Only monsters 15–30 m from the commander, at most 150 s per visit.
    /// </summary>
    private bool PilotClearYard(Player p, Vector2 me, Level level)
    {
        Monster boss = null;
        foreach (var m in level.StrongholdMobs)
            if (m != null && IsInstanceValid(m) && !m.Dead && (m.IsBoss || NamedHub.Of(m) != null)) { boss = m; break; }
        if (boss == null || boss.Aggro) return false;
        if (_yardLevel != level) { _yardLevel = level; _yardSince = _clock; }
        if (_clock - _yardSince > 150.0) return false;
        Monster pick = null; float bd = float.MaxValue; int left = 0;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || !IsInstanceValid(m) || m == boss || m.Faction != Faction.Monster) continue;
            float db = m.Pos2.DistanceTo(boss.Pos2);
            if (db < 15f || db > 30f) continue;
            bool mob = false;
            foreach (var s in level.StrongholdMobs) if (s == m) { mob = true; break; }
            if (mob && m.Pack >= 0 && m.Pack == boss.Pack) continue;       // the commander's own pack wakes with it
            left++;
            float d = m.Pos2.DistanceTo(me);
            if (d < bd) { bd = d; pick = m; }
        }
        if (pick == null) return false;
        if (_clock - _yardLog > 10.0) { _yardLog = _clock; Log.Info("pilot", $"stronghold: clears the yard first ({left} round {boss.Def?.Id}, {boss.Pos2.DistanceTo(me):0} m away)"); }
        // stop short of the commander's notice: walk to the monster, but not within 14.5 m of the commander
        Vector2 goal = pick.Pos2;
        if (goal.DistanceTo(boss.Pos2) < 14.5f) goal = boss.Pos2 + (goal - boss.Pos2).Normalized() * 14.5f;
        return Approach(p, me, goal, "stronghold yard", 4f);
    }
}

public partial class TestBot
{
    private double _unsafeLog = -10;

    /// <summary>
    /// A named encounter's "stand here" waits while the hero is swarmed or nearly out (low life and no potion): survival
    /// first, the hint again once it has backed off (astromancer, release re-run: 「貝拉克」's stand-behind-the-wall spot
    /// held at 30 % life, no potions, 20 attackers → death; the rare was still at full life).
    /// </summary>
    private bool PilotHintUnsafe(Player p, Vector2 me)
    {
        bool bad = Pressure(me, 7f) >= 6 || p.LifeFrac < 0.5f && p.Potions == 0;
        if (bad && _clock - _unsafeLog > 8.0) { _unsafeLog = _clock; Log.Info("pilot", $"named: holds off the stand spot (life {p.LifeFrac * 100f:0}%, potions {p.Potions}, pressure {Pressure(me, 7f)})"); }
        return bad;
    }
}

public partial class TestBot
{
    private readonly List<Vector2> _trail = new();
    private double _trailAt = -10;
    private Node _trailRoot;

    /// <summary>Every 2 s a spot where no monster is awake within 15 m and none sleeps within 12 m (the ground it cleared).</summary>
    private void SafeTrail(Vector2 me)
    {
        if (_trailRoot != World.Root) { _trailRoot = World.Root; _trail.Clear(); }
        if (_clock - _trailAt < 2.0) return;
        _trailAt = _clock;
        if (CloseMonsters(me, 15f) > 0 || SleepersNear(me, 12f) > 0) return;
        if (_trail.Count > 0 && _trail[^1].DistanceTo(me) < 4f) return;
        _trail.Add(me);
        if (_trail.Count > 24) _trail.RemoveAt(0);
    }

    /// <summary>The newest cleared spot 8–40 m away (a back-off leans that way: a lv1 astromancer backing off into
    /// unexplored ground grew a 20-monster crowd to 28, release re-run).</summary>
    private Vector2? SafeSpot(Vector2 me)
    {
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            float d = _trail[i].DistanceTo(me);
            if (d >= 8f && d <= 40f) return _trail[i];
        }
        return null;
    }
}
