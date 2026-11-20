using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// --suite=anims: the animation audit. Static part: every clip the game asks for exists in the shared library, loops
/// when it must, and reaches the bones of every character model (heroes, forms, NPCs, monsters, bosses) so nobody
/// stands in a bind pose; attack timings fit their clips; the stride table matches the clips. Live part: the hero of
/// every class idles, runs (right clip, legs moving, feet matched to the ground, facing where it goes), stops, rolls,
/// attacks with every skill, flinches, dies and gets up; every monster rises, walks or runs, attacks and dies; town
/// NPCs play their idles. It ends with the session's AnimCheck counters.
/// </summary>
public sealed partial class TestSuite
{
    private static readonly string[] HeroCodeClips =
    {
        "Idle_Loop", "Sword_Idle", "Idle_Shield_Loop", "Pistol_Idle_Loop", "Zombie_Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Roll",
        "Hit_Chest", "Death01", "Melee_Hook", "Zombie_Scratch", "NinjaJump_Idle_Loop", "NinjaJump_Land",
    };
    private static readonly string[] MonsterCodeClips =
    {
        "LayToIdle", "Hit_Knockback", "Hit_Head", "Hit_Chest", "NinjaJump_Start", "NinjaJump_Land", "NinjaJump_Idle_Loop", "Spell_Simple_Shoot",
        "Spell_Simple_Enter", "Sword_Idle", "Death01",
    };
    private static readonly string[] NpcCodeClips = { "Idle_Loop", "Idle_Lantern_Loop", "Idle_FoldArms_Loop", "Idle_Talking_Loop", "Idle_Torch_Loop", "Sitting_Idle_Loop" };

    private static string HeroIdle(ClassId c) => c switch
    {
        ClassId.Emberblade or ClassId.Wildsoul => "Sword_Idle", ClassId.Lampknight => "Idle_Shield_Loop", ClassId.Hunter => "Pistol_Idle_Loop", _ => "Idle_Loop",
    };

    private CharModel _probe;

    private bool ClipOk(string clip, string usedBy, bool mustLoop = false)
    {
        if (string.IsNullOrEmpty(clip)) { Fail($"anim: {usedBy} names no clip"); return false; }
        if (_probe?.Anim == null || !_probe.Anim.HasAnimation(clip)) { Fail($"anim: clip '{clip}' used by {usedBy} is not in the shared library"); return false; }
        if (mustLoop && _probe.Anim.GetAnimation(clip).LoopMode == Animation.LoopModeEnum.None) { Fail($"anim: clip '{clip}' used by {usedBy} is held for a long time but does not loop"); return false; }
        return true;
    }

    private float ClipLen(string clip) => _probe != null && _probe.Has(clip) ? _probe.Len(clip) : 0f;

    /// <summary>Ground speed of a locomotion clip at rate 1 and scale 1, from the travel of the feet while they are planted.</summary>
    private static float MeasureStride(CharModel m, string clip, out string detail)
    {
        detail = "";
        int fl = m.BoneIndex("foot_l"), fr = m.BoneIndex("foot_r");
        if (fl < 0 || fr < 0 || !m.Has(clip)) { detail = "no foot bones"; return 0f; }
        float len = m.Len(clip);
        const int N = 72;
        var feet = new[] { new Vector3[N + 1], new Vector3[N + 1] };
        m.Play(clip, 1f, 0f, true);
        for (int i = 0; i <= N; i++)
        {
            m.Anim.Seek(len * i / N, true);
            feet[0][i] = m.BoneInModel(fl); feet[1][i] = m.BoneInModel(fr);
        }
        double sum = 0; int n = 0; float zRange = 0f;
        foreach (var f in feet)
        {
            float yMin = f.Min(v => v.Y), yMax = f.Max(v => v.Y);
            zRange = Mathf.Max(zRange, f.Max(v => v.Z) - f.Min(v => v.Z));
            float planted = yMin + Mathf.Max(0.015f, (yMax - yMin) * 0.12f);
            for (int i = 0; i < N; i++)
            {
                if (f[i].Y > planted || f[i + 1].Y > planted) continue;
                sum += -(f[i + 1].Z - f[i].Z) / (len / N);       // a planted foot travels backward (−Z) under a body walking toward +Z
                n++;
            }
        }
        float planted2 = n > 0 ? (float)(sum / n) : 0f;
        // the planted foot moves back at ground speed for the whole stance: that plateau is the upper end of the backward
        // speeds over a cycle (the mean over "low" samples is dragged down by the foot skimming forward near the ground)
        var back = new List<float>();
        foreach (var f in feet) for (int i = 0; i < N; i++) back.Add(-(f[i + 1].Z - f[i].Z) / (len / N));
        back.Sort();
        // stance speed per foot: the fastest backward samples that together cover 70 % of the foot's travel
        float p85 = 0f;
        foreach (var f in feet)
        {
            var v = new List<float>();
            for (int i = 0; i < N; i++) v.Add(-(f[i + 1].Z - f[i].Z) / (len / N));
            v.Sort(); v.Reverse();
            float range = f.Max(q => q.Z) - f.Min(q => q.Z), dist = 0f, time = 0f;
            foreach (float x in v) { if (x <= 0f || dist >= range * 0.7f) break; dist += x * (len / N); time += len / N; }
            p85 += time > 0f ? dist / time * 0.5f : 0f;
        }
        detail = $"cycle {len:0.00}s, foot travel {zRange:0.00}m, backward speed p60 {back[(int)(back.Count * 0.6f)]:0.00} p75 {back[(int)(back.Count * 0.75f)]:0.00} stance {p85:0.00} p93 {back[(int)(back.Count * 0.93f)]:0.00} max {back[^1]:0.00} m/s, mean while low {planted2:0.00} m/s over {n} samples";
        return p85;
    }

    /// <summary>When the striking hand moves fastest in a clip: the moment a blow should land (seconds at rate 1).</summary>
    private static float StrikeTime(CharModel m, string clip, out float peak)
    {
        peak = 0f;
        int hr = m.BoneIndex("hand_r"), hl = m.BoneIndex("hand_l"), fr = m.BoneIndex("foot_r");
        if (hr < 0 || !m.Has(clip)) return -1f;
        float len = m.Len(clip);
        int n = Math.Max(8, (int)(len * 60f));
        var bonesToTrack = new[] { hr, hl, fr };
        var prev = new Vector3[3];
        var speed = new float[n + 1];
        m.Play(clip, 1f, 0f, true);
        for (int i = 0; i <= n; i++)
        {
            m.Anim.Seek(len * i / n, true);
            float best = 0f;
            for (int b = 0; b < 3; b++)
            {
                if (bonesToTrack[b] < 0) continue;
                var pos = m.BoneInModel(bonesToTrack[b]);
                if (i > 0) best = Mathf.Max(best, pos.DistanceTo(prev[b]) / (len / n));
                prev[b] = pos;
            }
            speed[i] = best;
            peak = Mathf.Max(peak, best);
        }
        // the first swing: the first local maximum that reaches 70 % of the fastest moment
        for (int i = 1; i < n; i++)
            if (speed[i] >= peak * 0.7f && speed[i] >= speed[i - 1] && speed[i] >= speed[i + 1]) return len * i / n;
        return len * 0.5f;
    }

    private IEnumerator AnimsSuite()
    {
        _section = "anims: library";
        yield return StartHero(ClassId.Emberblade, 20, "town", gear: true, god: true);
        int err0 = Log.ErrorCount;
        var root = World.Root;
        _probe = CharModel.Create("hero_emberblade");
        root.AddChild(_probe);
        _probe.Position = new Vector3(0, -30, 0);
        yield return null;
        if (_probe.Anim == null || _probe.Skeleton == null) { Fail("anim: the reference hero model has no skeleton or animation player; nothing can be audited"); yield break; }

        var clips = _probe.Anim.GetAnimationList().Select(x => x.ToString()).OrderBy(x => x).ToList();
        Log.Info("suite", $"anim: library has {clips.Count} clip names; looping: {string.Join(" ", clips.Where(c => _probe.Anim.GetAnimation(c).LoopMode != Animation.LoopModeEnum.None))}");
        var bones = new List<string>();
        for (int i = 0; i < _probe.Skeleton.GetBoneCount(); i++) bones.Add(_probe.Skeleton.GetBoneName(i));
        Log.Info("suite", $"anim: reference skeleton has {bones.Count} bones: {string.Join(" ", bones.Take(80))}{(bones.Count > 80 ? " …" : "")}");
        Check(_probe.BoneIndex("hand_r") >= 0 && _probe.BoneIndex("DEF-hand.R") >= 0 && _probe.BoneIndex("foot_l") >= 0 && _probe.BoneIndex("head") >= 0,
            "anim: the bones that effects and checks attach to (right hand, feet, head) resolve on the shared rig");
        Log.Info("suite", "anim: clip lengths: " + string.Join(" ", clips.Where(c => !c.EndsWith("_Loop") || !clips.Contains(c[..^5])).Select(c => $"{c}={_probe.Len(c):0.00}")));

        // ── when does each attack clip strike? ──
        var strike = new Dictionary<string, float>();
        var peaks = new Dictionary<string, float>();
        {
            var attackClips = new HashSet<string>();
            foreach (var s in SkillDb.All) { attackClips.Add(s.Anim); if (s.AnimAlt != null) foreach (var a in s.AnimAlt) attackClips.Add(a); }
            foreach (var d in MonsterDb.All) foreach (var a in d.AttackAnims ?? Array.Empty<string>()) attackClips.Add(a);
            var sb = new System.Text.StringBuilder();
            foreach (var c in attackClips.OrderBy(x => x))
            {
                if (!_probe.Has(c)) continue;
                float t = StrikeTime(_probe, c, out float peak);
                strike[c] = t; peaks[c] = peak;
                sb.Append($" {c}={t:0.00}s/{_probe.Len(c):0.00}s(peak {peak:0.0} m/s)");
            }
            Log.Info("suite", "anim: first strike of each attack clip (time of the fastest hand or foot / clip length):" + sb);
            _probe.Play("Idle_Loop", 1f, 0f, true);
        }
        // the strike table the game aligns attacks with must match the clips
        foreach (var kv in CharModel.Strike)
        {
            if (!strike.TryGetValue(kv.Key, out float st)) { if (!_probe.Has(kv.Key)) Fail($"anim strike: '{kv.Key}' is in the strike table but not in the library"); continue; }
            if (Mathf.Abs(st - kv.Value) > 0.07f) Fail($"anim strike: '{kv.Key}' strikes at {st:0.00}s but the table says {kv.Value:0.00}s");
        }
        var early = new List<string>(); var late = new List<string>();
        void Timing(string who, string clip, float speed, float contactIn)
        {
            if (!strike.TryGetValue(clip, out float st) || st < 0f || !peaks.TryGetValue(clip, out float pk) || pk < 5f) return;   // slow casting poses have no strike
            if (clip is "NinjaJump_Start" or "Shield_Dash" or "Sword_Dash" or "Sword_Block") return;                               // movement clips
            float diff = CharModel.ClipTimeAtContact(clip, speed, contactIn) - st;
            if (diff < -0.15f) early.Add($"{who}:{clip} lands {-diff:0.00}s before the swing{(CharModel.Strike.ContainsKey(clip) ? "" : " (clip not in the strike table)")}");
            else if (diff > 0.2f) late.Add($"{who}:{clip} lands {diff:0.00}s after the swing{(CharModel.Strike.ContainsKey(clip) ? "" : " (clip not in the strike table)")}");
        }
        foreach (var s in SkillDb.All)
        {
            if (s.Kind is SkillKind.Channel or SkillKind.Buff or SkillKind.Summon) continue;
            Timing(s.Id, s.Anim, s.AnimSpeed, s.Contact);
            if (s.AnimAlt != null) foreach (var a in s.AnimAlt) Timing(s.Id, a, s.AnimSpeed, s.Contact);
        }
        foreach (var d in MonsterDb.All)
            if (d.AI != MonAI.Boss && d.AI != MonAI.Exploder)
                foreach (var a in d.AttackAnims ?? Array.Empty<string>()) Timing(d.Id, a, d.AnimSpeed, d.Contact);
        if (early.Count + late.Count == 0) Pass($"anim timing: every skill and monster blow lands on its swing once aligned ({CharModel.Strike.Count} clips in the strike table)");
        else Fail($"anim timing: {early.Count} blows land before their swing and {late.Count} after it — {string.Join(" | ", early.Concat(late))}");

        // ── clips named in code and data ──
        _section = "anims: references";
        int fails0 = _fail;
        foreach (var c in HeroCodeClips) ClipOk(c, "the hero controller", c.EndsWith("_Loop") || c == "Sword_Idle");
        foreach (var c in MonsterCodeClips) ClipOk(c, "the monster / boss controller", c.EndsWith("_Loop") || c == "Sword_Idle");
        foreach (var c in NpcCodeClips) ClipOk(c, "town people and prisoners", true);
        int skillRefs = 0, timing = 0;
        foreach (var s in SkillDb.All)
        {
            var names = new List<string> { s.Anim };
            if (s.AnimAlt != null) names.AddRange(s.AnimAlt);
            foreach (var a in names.Distinct())
            {
                skillRefs++;
                if (!ClipOk(a, $"skill {s.Id}", s.Kind == SkillKind.Channel && false)) continue;
                if (s.Kind == SkillKind.Channel) continue;
                float len = ClipLen(a), contactAt = s.Contact * s.AnimSpeed, lockAt = s.Lock * s.AnimSpeed;
                if (contactAt > len + 0.02f) { timing++; Fail($"anim timing: skill {s.Id} strikes at {contactAt:0.00}s of '{a}', which is only {len:0.00}s long (the blow lands after the swing has ended)"); }
                else if (lockAt > len + 0.35f) { timing++; Log.Warn("suite", $"anim timing: skill {s.Id} holds the hero {lockAt - len:0.00}s past the end of '{a}' ({len:0.00}s at speed {s.AnimSpeed})"); }
            }
        }
        int monRefs = 0;
        foreach (var d in MonsterDb.All)
        {
            ClipOk(d.Idle, $"monster {d.Id} (idle)", true);
            ClipOk(d.Walk, $"monster {d.Id} (walk)", true);
            ClipOk(d.Run, $"monster {d.Id} (run)", true);
            if (!CharModel.IsLocomotion(d.Walk)) Fail($"anim: monster {d.Id} walks with '{d.Walk}', which has no stride entry (its feet cannot be matched to the ground)");
            if (!CharModel.IsLocomotion(d.Run)) Fail($"anim: monster {d.Id} runs with '{d.Run}', which has no stride entry");
            foreach (var a in d.AttackAnims ?? Array.Empty<string>())
            {
                monRefs++;
                if (!ClipOk(a, $"monster {d.Id} (attack)")) continue;
                float len = ClipLen(a), contactAt = d.Contact * d.AnimSpeed;
                if (d.AI != MonAI.Boss && d.AI != MonAI.Exploder && contactAt > len + 0.02f) { timing++; Fail($"anim timing: monster {d.Id} strikes at {contactAt:0.00}s of '{a}', which is only {len:0.00}s long"); }
            }
            if (d.AI == MonAI.Boss)
            {
                var kit = BossKits.Get(d.Id);
                var moves = new List<BossMoveDef>();
                if (kit?.Basic != null) moves.Add(kit.Basic);
                if (kit?.Moves != null) moves.AddRange(kit.Moves);
                foreach (var mv in moves)
                {
                    var names = mv.Anims != null && mv.Anims.Length > 0 ? mv.Anims : new[] { mv.Anim };
                    foreach (var a in names)
                    {
                        monRefs++;
                        if (!ClipOk(a, $"boss {d.Id} move {mv.Id}")) continue;
                        if (mv.Contact > ClipLen(a) + 0.02f) { timing++; Fail($"anim timing: boss {d.Id} move {mv.Id} lands at {mv.Contact:0.00}s of '{a}' ({ClipLen(a):0.00}s long)"); }
                    }
                }
            }
        }
        if (_arg == "static") { /* the quick part ends after the model check below */ }
        Check(_fail == fails0, $"anim: every clip named by code and data exists ({HeroCodeClips.Length + MonsterCodeClips.Length + NpcCodeClips.Length} in code, {skillRefs} skill clips, {monRefs} monster and boss clips) and attack timings fit", $"timing problems={timing}");
        yield return null;

        // ── stride table against the clips ──
        _section = "anims: stride";
        foreach (var kv in CharModel.Stride.OrderBy(k => k.Value))
        {
            if (!_probe.Has(kv.Key)) { Fail($"anim stride: '{kv.Key}' is in the stride table but not in the library"); continue; }
            float v = MeasureStride(_probe, kv.Key, out string detail);
            bool used = MonsterDb.All.Any(d => d.Walk == kv.Key || d.Run == kv.Key) || kv.Key is "Walk_Loop" or "Jog_Fwd_Loop" or "Sprint_Loop";
            bool ok = v > 0.05f && Mathf.Abs(v - kv.Value) <= Mathf.Max(0.15f, kv.Value * 0.2f);
            string line = $"anim stride: {kv.Key} measured {v:0.00} m/s forward (table {kv.Value:0.00}) — {detail}";
            if (ok) Pass(line);
            else if (v <= 0.05f && used) Fail(line + (v < -0.05f ? " — the feet travel the wrong way: the model walks backward" : " — no forward travel measured"));
            else if (used) Fail(line + " — the table is off by more than 20 %: feet will slide");
            else Log.Warn("suite", line + " (not used by the game)");
        }
        _probe.Play("Idle_Loop", 1f, 0f, true);
        yield return null;

        // ── every character model: rigged, tracks reach its bones, not in the bind pose ──
        _section = "anims: models";
        var ids = new List<string>();
        foreach (var k in Names.ClassKeys) ids.Add("hero_" + k);
        ids.Add("form_bear"); ids.Add("form_wolf");
        ids.AddRange(new[] { "npc_elder", "npc_smith", "npc_merchant", "npc_occultist", "npc_villager_m", "npc_villager_f" });
        foreach (var d in MonsterDb.All) if (!ids.Contains(d.Model)) ids.Add(d.Model);
        int standIns = 0, badModels = 0;
        var standInNames = new List<string>();
        foreach (var id in ids)
        {
            var m = CharModel.Create(id);
            root.AddChild(m);
            m.Position = new Vector3(4, -30, 0);
            yield return null;
            string what = $"anim model {id}{(m.IsStandIn ? " (stand-in)" : m.IsPlaceholder ? " (placeholder)" : "")}";
            if (m.IsStandIn || m.IsPlaceholder) { standIns++; standInNames.Add(id); }
            if (m.Anim == null || m.Skeleton == null) { badModels++; Fail($"{what}: no skeleton or animation player — it would stand in its bind pose"); m.QueueFree(); continue; }
            var problems = new List<string>();
            foreach (var clip in new[] { "Idle_Loop", "Jog_Fwd_Loop", "Sword_Regular_A", "Death01" })
            {
                var (ok, total) = m.TrackCoverage(clip);
                if (total == 0 || ok < total * 0.9f) problems.Add($"'{clip}' reaches {ok}/{total} bones");
                m.Play(clip, 1f, 0f, true);
                m.Anim.Seek(m.Len(clip) * 0.4f, true);
                float off = m.PoseOffRest();
                if (off < 0.02f) problems.Add($"'{clip}' leaves the bind pose untouched (off-rest {off:0.000})");
            }
            if (m.Height < 0.6f || m.Height > 6f) problems.Add($"height {m.Height:0.00} m");
            if (problems.Count > 0) { badModels++; Fail($"{what}: {string.Join("; ", problems)}"); }
            m.QueueFree();
        }
        Check(badModels == 0, $"anim: all {ids.Count} character models are rigged and every probe clip moves their skeleton", $"{standIns} still use stand-ins: {string.Join(" ", standInNames)}");
        yield return null;

        if (_arg == "static")
        {
            if (_probe != null && GodotObject.IsInstanceValid(_probe)) _probe.QueueFree();
            _probe = null;
            yield break;
        }
        if (_arg == "monsters")
        {
            _section = "anims: monsters";
            yield return StartHero(ClassId.Lampknight, 30, "wastes", gear: true, god: true);
            yield return MonsterAnimTest();
            Check(AnimCheck.Clean, AnimCheck.Report());
            if (_probe != null && GodotObject.IsInstanceValid(_probe)) _probe.QueueFree();
            _probe = null;
            yield break;
        }

        // ── town people ──
        _section = "anims: npcs";
        yield return 0.6f;
        int npcBad = 0, npcN = 0;
        var npcNotes = new List<string>();
        foreach (var n in G.Level.Npcs)
        {
            if (n?.Model == null || !GodotObject.IsInstanceValid(n.Model)) continue;
            npcN++;
            float off = n.Model.PoseOffRest();
            bool ok = n.Model.Playing && n.Model.Current.Length > 0 && off > 0.02f;
            npcNotes.Add($"{n.Id}:{(n.Model.Current.Length > 0 ? n.Model.Current : "(none)")}{(ok ? "" : " NOT ANIMATING")}");
            if (!ok) npcBad++;
        }
        Check(npcN > 0 && npcBad == 0, $"anim: all {npcN} people in town play an idle (nobody in a bind pose)", string.Join(" ", npcNotes));
        // each named NPC holds the clip its model was built for (lantern, hammer, folded arms) — also while talking
        {
            var want = new Dictionary<string, string> { ["elder"] = "Idle_Lantern_Loop", ["smith"] = "Idle_FoldArms_Loop", ["occultist"] = "Idle_FoldArms_Loop", ["merchant"] = "Idle_Talking_Loop" };
            var wrong = new List<string>();
            foreach (var n in G.Level.Npcs)
                if (n?.Model != null && GodotObject.IsInstanceValid(n.Model) && want.TryGetValue(n.Id, out var clip) && n.Model.Current != clip) wrong.Add($"{n.Id} plays {n.Model.Current}, wants {clip}");
            foreach (var id in new[] { "elder", "smith" })
            {
                var n = G.Level.Npcs.FirstOrDefault(x => x.Id == id);
                if (n?.Obj == null || !GodotObject.IsInstanceValid(n.Obj)) { wrong.Add($"{id} is not in town"); continue; }
                Log.Guard("npc", () => n.Obj.OnUse?.Invoke(World.P));
                yield return 0.6f;
                if (n.Model.Current != want[id] || !n.Model.Playing) wrong.Add($"{id} switches to {n.Model.Current} while talking");
                Log.Guard("ui", () => G.Ui?.CloseAll());
                G.PauseLocks = 0;
                yield return 0.3f;
                if (n.Model.Current != want[id]) wrong.Add($"{id} plays {n.Model.Current} after the talk");
            }
            Check(wrong.Count == 0, "anim: elder keeps the lantern clip, smith and occultist fold their arms, merchant talks — before, during and after a talk", string.Join("; ", wrong));
        }
        // do they keep moving — also behind an open panel, when the game is paused?
        {
            var people = G.Level.Npcs.Where(n => n?.Model != null && GodotObject.IsInstanceValid(n.Model)).ToList();
            var s0 = people.Select(n => n.Model.PoseSignature()).ToList();
            yield return 0.6f;
            var s1 = people.Select(n => n.Model.PoseSignature()).ToList();
            Log.Guard("ui", () => G.Ui?.Open("quests"));
            yield return 0.3f;
            bool paused = G.Paused;
            var s2 = people.Select(n => n.Model.PoseSignature()).ToList();
            yield return 0.7f;
            var s3 = people.Select(n => n.Model.PoseSignature()).ToList();
            Log.Guard("ui", () => G.Ui?.CloseAll());
            G.PauseLocks = 0;
            int still = 0, stillPaused = 0;
            for (int i = 0; i < people.Count; i++)
            {
                if (Mathf.Abs(s1[i] - s0[i]) < 1e-4f) still++;
                if (Mathf.Abs(s3[i] - s2[i]) < 1e-4f) stillPaused++;
            }
            Check(people.Count > 0 && still == 0 && stillPaused == 0, $"anim: the people of the town keep moving, also behind an open panel (game paused={paused})",
                $"frozen while playing: {still}/{people.Count}; frozen behind the quest log: {stillPaused}/{people.Count}");
            yield return 0.2f;
        }

        // ── the hero of every class, live ──
        foreach (ClassId cls in Enum.GetValues<ClassId>())
        {
            _section = $"anims: hero {Names.ClassKey(cls)}";
            yield return HeroAnimTest(cls);
        }

        // ── every monster, live ──
        _section = "anims: monsters";
        yield return StartHero(ClassId.Lampknight, 30, "wastes", gear: true, god: true);
        yield return MonsterAnimTest();

        _section = "anims: summary";
        int newErrors = Log.ErrorCount - err0;
        Check(AnimCheck.Clean, AnimCheck.Report());
        Check(newErrors == 0, "anim: no errors (missing clips, failed updates) were logged during the audit", $"errors +{newErrors}");
        if (_probe != null && GodotObject.IsInstanceValid(_probe)) _probe.QueueFree();
        _probe = null;
    }

    private IEnumerator HeroAnimTest(ClassId cls)
    {
        yield return StartHero(cls, 50, "town", gear: true, god: false);
        var h = G.Hero; var p = G.Player;
        string tag = Names.ClassKey(cls);
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
        p.Teleport(arena);
        p.FaceDir(Vector2.Down);
        yield return 0.7f;
        string idle = HeroIdle(cls);
        var m = p.ActiveModel;
        Check(m.Current == idle && m.Playing && m.PoseOffRest() > 0.02f, $"anim {tag}: standing plays '{idle}'", $"clip={m.Current} playing={m.Playing} offRest={m.PoseOffRest():0.00}{(m.IsStandIn ? " (stand-in model)" : "")}");

        // run east, then north: clip, legs, speed match, facing
        foreach (var dir in new[] { new Vector2(1, 0), new Vector2(0, -1) })
        {
            Vector2 goal = World.Grid.ClampLine(p.Pos2, p.Pos2 + dir * 9f, false);
            p.SetMove(goal, false);
            yield return 0.45f;
            Vector2 a = p.Pos2; float s0 = m.LegSignature(); double t0 = G.Time;
            yield return 0.2f;
            float s1 = m.LegSignature();
            yield return 0.3f;
            float secs = (float)(G.Time - t0);
            float v = secs > 0.01f ? p.Pos2.DistanceTo(a) / secs : 0f;
            string clip = m.Current;
            float stride = CharModel.Stride.GetValueOrDefault(clip) * m.Scale.X * m.PlayRate;
            float slip = stride > 0.01f ? v / stride : 0f;
            float facing = p.Forward.Dot(dir);
            bool ok = CharModel.IsLocomotion(clip) && m.Playing && Mathf.Abs(s1 - s0) > 1e-4f && v > 2f && slip > 0.8f && slip < 1.25f && facing > 0.9f;
            Check(ok, $"anim {tag}: running {(dir.X > 0 ? "east" : "north")} plays a run cycle that matches the ground", $"clip={clip} rate={m.PlayRate:0.00} speed={v:0.0} m/s feet={stride:0.0} m/s (ratio {slip:0.00}) legs moving={Mathf.Abs(s1 - s0) > 1e-4f} facing·dir={facing:0.00}");
            yield return Until(() => p.Pos2.DistanceTo(goal) < 0.6f, 4, "the hero to arrive");
            yield return 0.45f;
            Check(m.Current == idle && m.Playing, $"anim {tag}: stopping returns to '{idle}'", $"clip={m.Current}");
        }

        // roll
        p.Teleport(arena);
        yield return 0.1f;
        p.EvadeCd = 0f;
        p.QueueEvade();
        yield return 0.12f;
        string evadeClip = p.Form != 0 ? "Roll" : Player.EvadeAnimOf(p.Hero.Class).clip;      // [gfx 0.3, item 5] robed heroes step-dash instead of rolling
        bool rolled = m.Current == evadeClip && p.Busy;
        yield return 0.7f;
        Check(rolled && m.Current == idle, $"anim {tag}: evading plays '{evadeClip}', then idles again", $"during={rolled} after={m.Current}");

        // every skill: the cast shows its clip, then the hero returns to idle (or keeps running), never frozen in the last frame
        p.Teleport(arena);
        int mlvl = h.D.Level;
        var skills = SkillDb.Of(cls).OrderBy(s => s.UnlockLevel).ToList();
        var wrong = new List<string>();
        foreach (var s in skills)
        {
            Configure(h, s, null, "base", 1);
            ClearField();
            p.CancelAction(); p.SetForm(0); p.Teleport(arena); p.Cds.Clear(); p.StealthLeft = 0f;
            yield return null;
            Dummy("cultist", mlvl, arena + new Vector2(2.2f, 0f), 5e7f);
            if (s.Kind == SkillKind.Command)
            {
                var sum = SkillDb.Of(cls).FirstOrDefault(o => o.Kind == SkillKind.Summon && o.Summon != null && o.Summon.StartsWith("pact"));
                if (sum != null) { h.D.Bar[3] = sum.Id; h.Recalc(); p.Resource = h.MaxResource; p.TryCast(3, arena + new Vector2(1.5f, 1.5f), null, false); yield return 0.1f; yield return Until(() => !p.Busy, 4, "fiend summon"); }
            }
            var d = h.Eff(s.Id);
            p.Resource = h.MaxResource; p.Pips = h.MaxPips;
            var target = World.Nearest(Faction.Player, p.Pos2, 30f);
            var expected = new HashSet<string> { d.Anim };
            if (d.AnimAlt != null) foreach (var a in d.AnimAlt) expected.Add(a);
            if (cls == ClassId.Wildsoul) { expected.Add("Melee_Hook"); expected.Add("Zombie_Scratch"); }
            var seen = new HashSet<string>();
            if (d.Kind == SkillKind.Channel) p.ForceHold = 2;
            bool cast = p.TryCast(2, target != null ? target.Pos2 : arena + new Vector2(2, 0), target, true);
            if (cast && p.Busy) seen.Add(p.ActiveModel.Current);
            for (int i = 0; i < 12 && (p.Busy || i < 3); i++) { yield return 0.05f; if (p.Busy) seen.Add(p.ActiveModel.Current); }
            p.ForceHold = -1;
            yield return Until(() => !p.Busy, 4, $"{s.Id} to finish");
            yield return 0.5f;
            string after = p.ActiveModel.Current;
            string wantIdle = p.Form != 0 ? "Zombie_Idle_Loop" : idle;
            bool shown = seen.Overlaps(expected) || d.Kind is SkillKind.Leap or SkillKind.Dash or SkillKind.Blink && seen.Count > 0;
            if (!cast) wrong.Add($"{s.Id}: could not be cast");
            else if (!shown) wrong.Add($"{s.Id}: expected {string.Join("/", expected)} but saw [{string.Join(",", seen)}]");
            else if (after != wantIdle && !CharModel.IsLocomotion(after)) wrong.Add($"{s.Id}: afterwards the hero is left in '{after}' instead of '{wantIdle}'");
            else if (!p.ActiveModel.Playing) wrong.Add($"{s.Id}: afterwards nothing is playing");
        }
        p.SetForm(0);
        Check(wrong.Count == 0, $"anim {tag}: all {skills.Count} skills show their attack clip and return to idle", string.Join(" | ", wrong));

        // flinch, death, getting up
        ClearField();
        h.Buffs.Clear();
        Game.AutoBuild(h);
        p.Teleport(arena);
        yield return 0.3f;
        p.ApplyStatus(St.Stun(0.6f), null);
        yield return 0.2f;
        string stun = p.ActiveModel.Current;
        yield return 0.9f;
        Check(stun == "Hit_Chest" && p.ActiveModel.Current == idle, $"anim {tag}: a stun shows the hit reaction, then idle", $"during={stun} after={p.ActiveModel.Current}");
        for (int tries = 0; tries < 3 && !p.Dead; tries++)        // a second wind or a deathless buff may refuse the first blow
        {
            p.IFrames = 0f; p.Barrier = 0f; p.SecondWindCd = 999f;
            h.Buffs.Clear();
            p.TakeHit(new Hit { Source = null, Amount = p.MaxLife * 50f, El = Element.Shadow });
        }
        yield return 0.3f;
        string dead = p.ActiveModel.Current;
        yield return 2.2f;
        float lying = p.ActiveModel.PoseOffRest();
        bool stays = p.ActiveModel.Current == "Death01";
        G.Respawn();
        yield return 0.6f;
        Check(p.Dead == false && dead == "Death01" && stays && p.ActiveModel.Current is "Idle_Loop" or "Sword_Idle" or "Idle_Shield_Loop" or "Pistol_Idle_Loop",
            $"anim {tag}: death plays 'Death01' and stays down; respawning stands up again", $"at death={dead} stays={stays} offRest={lying:0.0} after respawn={p.ActiveModel.Current}");
        Log.Guard("ui", () => G.Ui?.CloseAll());
    }

    private IEnumerator MonsterAnimTest()
    {
        var p = G.Player;
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 13);
        p.Teleport(arena);
        yield return 0.2f;
        var defs = MonsterDb.All.Where(d => d.AI != MonAI.Boss).ToList();
        int bad = 0;
        const int batch = 6;
        for (int b0 = 0; b0 < defs.Count; b0 += batch)
        {
            ClearField();
            p.Teleport(arena); p.Life = p.MaxLife;
            yield return null;
            var group = defs.Skip(b0).Take(batch).ToList();
            var mons = new List<Monster>();
            var faces = new List<List<float>>();
            var st = new List<(bool rise, bool move, bool attack, List<float> slip, float facing, HashSet<string> clips, Vector2 last, string wrongMove)>();
            for (int i = 0; i < group.Count; i++)
            {
                // somewhere on the ring from which the hero can actually be walked to (the open ground ends before the ring does)
                Vector2 at = arena + Vector2.Right * 21f;
                for (int k = 0; k < 24; k++)
                {
                    at = World.Grid.NearestWalk(arena + Vector2.Right.Rotated(i * Mathf.Tau / group.Count + k * Mathf.Tau / 24f) * (21f - (k / 8) * 4f));
                    if (World.Grid.WalkAt(at) && at.DistanceTo(arena) > 14f && (World.Grid.LineWalk(at, arena) || World.Grid.Path(at, arena).Count > 1)) break;
                }
                var m = Monster.Spawn(group[i], 30, MonTier.Normal, at, Faction.Monster);
                m.MaxLife = 5e7f; m.Life = 5e7f; m.NoLoot = true;
                mons.Add(m);
                faces.Add(new List<float>());
                st.Add((!group[i].RiseFromGround, false, false, new List<float>(), 1f, new HashSet<string>(), at, null));
            }
            yield return 0.25f;
            // buried ones must be lying still before they are woken
            for (int i = 0; i < mons.Count; i++)
                if (group[i].RiseFromGround && mons[i].Model.Current != "LayToIdle") { var s = st[i]; s.wrongMove = $"waits in '{mons[i].Model.Current}' instead of lying buried"; st[i] = s; }
            foreach (var m in mons) m.WakePack();
            double tEnd = G.Time + 12.0;
            double tPrev = G.Time;
            while (G.Time < tEnd)
            {
                yield return 0.1f;
                float secs = (float)(G.Time - tPrev); tPrev = G.Time;
                bool allDone = true;
                for (int i = 0; i < mons.Count; i++)
                {
                    var m = mons[i]; var s = st[i]; var d = group[i];
                    if (m == null || !GodotObject.IsInstanceValid(m) || m.Dead) continue;
                    string clip = m.Model.Current;
                    s.clips.Add(clip);
                    if (clip == "LayToIdle") s.rise = true;
                    if (d.AttackAnims.Contains(clip) || clip is "NinjaJump_Start" or "NinjaJump_Land") s.attack = true;
                    float moved = m.Pos2.DistanceTo(s.last);
                    float v = secs > 0.01f ? moved / secs : 0f;
                    bool jump = moved > Mathf.Max(1.2f, d.Speed * secs * 2.5f);        // a teleport or a leap, not a walk
                    if (v > 0.8f && m.GlobalPosition.Y < 0.05f && !m.Attacking && !jump)
                    {
                        if (CharModel.IsLocomotion(clip) && m.Model.Playing)
                        {
                            s.move = true;
                            float feet = CharModel.Stride[clip] * m.Model.Scale.X * m.Model.PlayRate;
                            if (feet > 0.01f && v < 12f) s.slip.Add(v / feet);
                            Vector2 dir = (m.Pos2 - s.last).Normalized();
                            s.facing = Mathf.Min(s.facing, m.Forward.Dot(dir));
                            faces[i].Add(m.Forward.Dot(dir));
                        }
                        else if (clip != "LayToIdle" && !clip.StartsWith("Hit_") && !clip.StartsWith("NinjaJump") && v > 1.8f && m.Knock.LengthSquared() < 0.05f)
                            s.wrongMove ??= $"moves at {v:0.0} m/s in '{clip}'{(m.Model.Playing ? "" : " (not playing)")}";
                    }
                    s.last = m.Pos2;
                    st[i] = s;
                    if (!(s.rise && s.move && s.attack)) allDone = false;
                }
                if (allDone && G.Time > tEnd - 6.5) break;
            }
            var endState = mons.Select(m => m != null && GodotObject.IsInstanceValid(m) ? m.DebugState : "freed").ToList();
            // deaths
            foreach (var m in mons) if (m != null && GodotObject.IsInstanceValid(m) && !m.Dead) { m.Barrier = 0; m.TakeHit(new Hit { Source = p, Amount = m.MaxLife * 20f, El = Element.Physical }); }
            yield return 0.15f;
            for (int i = 0; i < mons.Count; i++)
            {
                var m = mons[i]; var s = st[i]; var d = group[i];
                string death = m != null && GodotObject.IsInstanceValid(m) ? m.Model.Current : "(freed)";
                var problems = new List<string>();
                if (!s.rise) problems.Add("never rose from the ground (no LayToIdle)");
                if (!s.move) problems.Add($"never seen walking in a locomotion clip (clips seen: {string.Join(",", s.clips)})");
                if (!s.attack && d.AI != MonAI.Exploder)
                    problems.Add($"never showed an attack clip (wanted {string.Join("/", d.AttackAnims)}; saw {string.Join(",", s.clips)}); at the end: {(m != null && GodotObject.IsInstanceValid(m) ? endState[i] : "freed")}, hero at ({p.Pos2.X:0.0},{p.Pos2.Y:0.0})");
                if (s.wrongMove != null) problems.Add(s.wrongMove);
                float med = 0f;
                if (s.slip.Count > 0) { s.slip.Sort(); med = s.slip[s.slip.Count / 2]; if (med < 0.75f || med > 1.35f) problems.Add($"feet do not match the ground (speed ÷ stride = {med:0.00})"); }
                int badF = faces[i].Count(x => x < 0.3f);
                if (faces[i].Count >= 5 && badF > faces[i].Count * 0.15f) problems.Add($"walks without facing where it goes in {badF} of {faces[i].Count} samples (worst forward·motion {s.facing:0.00})");
                if (death != "Death01" && death != "(freed)" && d.AI != MonAI.Exploder) problems.Add($"dies in '{death}' instead of 'Death01'");
                string line = $"anim monster {d.Id} [{d.AI}, model {d.Model}{(m != null && GodotObject.IsInstanceValid(m) && m.Model.IsStandIn ? " stand-in" : "")}, walk {d.Walk} / run {d.Run} at {d.Speed:0.0} m/s]: clips {string.Join(",", s.clips)}; speed÷stride {med:0.00}; death {death}";
                if (problems.Count == 0) Pass(line); else { bad++; Fail(line + " | " + string.Join("; ", problems)); }
            }
        }
        // reactions: flinch on a solid hit, stun, freeze (pose held, then released), ash dissolve after death
        ClearField();
        p.Teleport(arena);
        yield return null;
        var r1 = Monster.Spawn(MonsterDb.Get("cultist"), 30, MonTier.Normal, arena + new Vector2(6f, 0f), Faction.Monster);
        r1.NoLoot = true; r1.WakePack();
        yield return 0.5f;
        r1.TakeHit(new Hit { Source = p, Amount = r1.MaxLife * 0.3f, El = Element.Physical, Dir = Vector2.Right });
        yield return 0.05f;
        string flinch = r1.Model.Current;
        yield return 0.6f;
        r1.CcImmune = 0f;
        r1.ApplyStatus(St.Stun(0.9f), p);
        yield return 0.25f;
        string stunned = r1.Model.Current;
        yield return 1.2f;
        r1.CcImmune = 0f;
        r1.ApplyStatus(St.Freeze(0.9f), p);
        yield return 0.3f;
        bool frozenStill = !r1.Model.Playing;
        yield return 1.3f;
        bool thawed = r1.Model.Playing;
        r1.TakeHit(new Hit { Source = p, Amount = r1.MaxLife * 20f, El = Element.Physical });
        yield return 0.1f;
        string died = r1.Model.Current;
        yield return 3.5f;
        bool gone = !GodotObject.IsInstanceValid(r1);
        Check(flinch.StartsWith("Hit_") && stunned == "Hit_Head" && frozenStill && thawed && died == "Death01" && gone,
            "anim: a monster flinches when hit hard, reels when stunned, holds still while frozen, thaws, dies and crumbles away",
            $"flinch={flinch} stun={stunned} frozen-still={frozenStill} thawed={thawed} death={died} freed={gone}");

        ClearField();
        Check(bad == 0, $"anim: all {defs.Count} monster and minion types rise, walk, attack and die with the right clips", $"with problems: {bad}");
    }
}
