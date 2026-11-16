using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// --suite=anims:shots-town | shots-heroes | shots-monsters (windowed, with --shot=&lt;base.png&gt;): close-up pictures of
/// characters in motion for a human to look at — the people of the town, the hero of every class standing, running
/// and striking, and every monster type walking in and fighting. What the audit proves with numbers, these show.
/// </summary>
public sealed partial class TestSuite
{
    private string ShotBase()
    {
        string s = G.ArgValue("shot");
        if (string.IsNullOrEmpty(s)) s = "Y:\\shots\\qa\\anim.png";
        return s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s;
    }

    private IEnumerator Snap(string name, float fov)
    {
        G.HarnessFov(fov);
        G.Cam?.Snap();
        yield return null;
        yield return null;
        G.HarnessShot($"{ShotBase()}_{name}.png");
        yield return null;
    }

    private IEnumerator AnimShots()
    {
        string mode = _arg;
        if (mode is "shots" or "shots-town")
        {
            _section = "shots: town";
            yield return StartHero(ClassId.Emberblade, 20, "town", gear: true, god: true);
            var p = G.Player;
            yield return 4.5f;          // let the area title card fade
            int k = 0;
            foreach (var n in G.Level.Npcs.ToList())
            {
                if (n?.Obj == null || !GodotObject.IsInstanceValid(n.Obj) || k >= 7) continue;
                // beside them, so both fit in a close frame
                Vector2 at = n.Obj.Pos2 + new Vector2(1.5f, 0.5f);
                if (!World.Grid.WalkAt(at)) at = n.Obj.Pos2 + new Vector2(-1.5f, 0.5f);
                if (!World.Grid.WalkAt(at)) at = G.Level.StandNear(n.Obj.Pos2, n.Obj.Pos2 + new Vector2(0, 3), 1.8f);
                p.Teleport(at);
                p.FaceDir(n.Obj.Pos2 - at);
                yield return 0.9f;
                yield return Snap($"npc{k}_{n.Id}", 16f);
                Log.Info("suite", $"shot npc {n.Id}: clip={n.Model?.Current} playing={n.Model?.Playing} offRest={n.Model?.PoseOffRest():0.0}");
                k++;
            }
            Check(k > 0, $"shots: {k} town people photographed");
        }
        // shots-hero-<class>: one class only, and every skill on its bar in mid-cast as well
        string only = mode != null && mode.StartsWith("shots-hero-") ? mode["shots-hero-".Length..] : null;
        if (mode is "shots" or "shots-heroes" || only != null)
        {
            foreach (ClassId cls in Enum.GetValues<ClassId>())
            {
                if (only != null && Names.ClassKey(cls) != only) continue;
                _section = $"shots: hero {Names.ClassKey(cls)}";
                yield return StartHero(cls, 30, "town", gear: true, god: true);
                var p = G.Player; var h = G.Hero;
                ClearField();
                Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
                p.Teleport(arena - new Vector2(4, 0));
                p.FaceDir(new Vector2(0.3f, 1f));
                yield return (cls == ClassId.Emberblade && mode == "shots-heroes") || only != null ? 4.5f : 0.8f;
                if (cls is ClassId.Emberblade or ClassId.Hunter or ClassId.Lampknight || only != null) yield return Snap($"hero_{Names.ClassKey(cls)}_idle", 11f);
                p.SetMove(arena + new Vector2(5, 0), false);
                yield return 0.5f;
                yield return Snap($"hero_{Names.ClassKey(cls)}_run", 11f);
                yield return Until(() => p.Pos2.DistanceTo(arena + new Vector2(5, 0)) < 0.7f, 4, "the hero to arrive");
                Dummy("cultist", 30, p.Pos2 + new Vector2(2.0f, 0.3f), 5e7f);
                yield return null;
                var target = World.Nearest(Faction.Player, p.Pos2, 10f);
                var basic = h.Eff(h.D.Bar[0]);
                p.TryCast(0, target.Pos2, target, true);
                yield return Mathf.Max(0.05f, (basic?.Contact ?? 0.2f) * 0.9f);
                yield return Snap($"hero_{Names.ClassKey(cls)}_attack", 11f);
                Log.Info("suite", $"shot hero {Names.ClassKey(cls)}: attack clip={p.ActiveModel.Current} stand-in={p.ActiveModel.IsStandIn}");
                yield return 0.5f;
                if (only != null)
                    for (int slot = 1; slot < h.D.Bar.Length; slot++)
                    {
                        var d = h.Eff(h.D.Bar[slot]);
                        if (d == null) continue;
                        yield return Until(() => !p.Busy, 3, "the hero to be free");
                        p.Resource = h.MaxResource; p.Pips = h.MaxPips;
                        target = World.Nearest(Faction.Player, p.Pos2, 12f);
                        if (target == null) break;
                        if (!p.TryCast(slot, target.Pos2, target, true)) { Log.Info("suite", $"shot hero {only}: {d.Id} could not be cast"); continue; }
                        yield return Mathf.Max(0.05f, d.Contact * 0.9f);
                        yield return Snap($"hero_{only}_{d.Id}", 11f);
                        Log.Info("suite", $"shot hero {only}: {d.Id} clip={p.ActiveModel.Current}");
                        yield return 0.9f;
                    }
            }
            Pass("shots: heroes photographed");
        }
        if (mode is "shots" or "shots-monsters")
        {
            _section = "shots: monsters";
            yield return StartHero(ClassId.Lampknight, 30, "wastes", gear: true, god: true);
            var p = G.Player;
            ClearField();
            Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 10);
            p.Teleport(arena);
            p.FaceDir(Vector2.Down);
            yield return 0.4f;
            var defs = MonsterDb.All.Where(d => d.AI != MonAI.Boss && d.Family != "minion").ToList();
            const int batch = 5;
            for (int b0 = 0; b0 < defs.Count; b0 += batch)
            {
                ClearField();
                p.Teleport(arena); p.Life = p.MaxLife;
                yield return null;
                var group = defs.Skip(b0).Take(batch).ToList();
                var mons = new List<Monster>();
                for (int i = 0; i < group.Count; i++)
                {
                    // a fan below the hero on screen (+Z), so the camera sees their faces as they come
                    float ang = Mathf.DegToRad(-50f + 100f * (group.Count == 1 ? 0.5f : i / (float)(group.Count - 1)));
                    Vector2 at = World.Grid.NearestWalk(arena + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * 9.5f);
                    var m = Monster.Spawn(group[i], 30, MonTier.Normal, at, Faction.Monster);
                    m.MaxLife = 5e7f; m.Life = 5e7f; m.NoLoot = true;
                    mons.Add(m);
                }
                yield return 0.4f;
                yield return Snap($"mon{b0 / batch}_wait", 30f);
                foreach (var m in mons) m.WakePack();
                yield return 1.5f;
                yield return Snap($"mon{b0 / batch}_walk", 26f);
                yield return 2.6f;
                yield return Snap($"mon{b0 / batch}_fight", 20f);
                Log.Info("suite", $"shot monsters {b0 / batch}: {string.Join(", ", mons.Where(GodotObject.IsInstanceValid).Select(m => $"{m.Def.Id}:{m.Model.Current}"))} (left to right as spawned)");
            }
            ClearField();
            Pass("shots: monsters photographed");
        }
        G.HarnessFov(0f);
    }
}
