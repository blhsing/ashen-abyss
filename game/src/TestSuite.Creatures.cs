using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// --suite=bestiary:creatures[:ids][:shots] (with --bestiary): the in-engine check of the rig B–F creatures, one at a
/// time on open floor in front of a deathless hero. For each: own clips, measured height against the generator's
/// (rigs_bf_meta.json "height" × the model's scale), ground contact (lowest skinned vertex in Idle, while walking and
/// after Death; hover / buried creatures judged by their own meta), the Hit reaction to a flinching blow, the Death clip
/// and the body clean-up. One `creature` line per model; FAIL lines name the defect.
/// </summary>
public sealed partial class TestSuite
{
    private sealed class BeastMeta { public float Height, Scale = 1f, Buried, FlyHeight; public bool Hover; public string Rig; }

    private static Dictionary<string, BeastMeta> ReadBeastMeta()
    {
        var res = new Dictionary<string, BeastMeta>();
        const string path = "res://assets/chars/rigs_bf_meta.json";
        if (!FileAccess.FileExists(path)) return res;
        using var doc = System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString(path));
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            var e = p.Value;
            float F(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? (float)v.GetDouble() : 0f;
            bool B(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.True;
            res[p.Name] = new BeastMeta
            {
                Height = F("height"), Scale = F("scale") > 0 ? F("scale") : 1f, Buried = F("buried"), FlyHeight = F("fly_height"), Hover = B("hover_in_model"),
                Rig = e.TryGetProperty("rig", out var r) ? r.GetString() : "?",
            };
        }
        return res;
    }

    /// <summary>Lowest and highest point of the posed (skinned) meshes, in metres above the actor's ground.</summary>
    private static (float low, float high) PosedExtent(Monster m) => m?.Model == null ? (0f, 0f) : PosedExtent(m.Model, m.GlobalPosition.Y);

    private static (float low, float high) PosedExtent(Node3D mod, float ground)
    {
        float low = float.MaxValue, high = float.MinValue;
        foreach (var mi in FindMeshes(mod))
        {
            if (!mi.Visible || mi.Mesh == null) continue;
            var skel = mi.Skin != null ? mi.GetNodeOrNull<Skeleton3D>(mi.Skeleton) : null;
            var xf = mi.GlobalTransform;
            Transform3D[] bind = null;
            if (skel != null)
            {
                int n = mi.Skin.GetBindCount();
                bind = new Transform3D[n];
                for (int i = 0; i < n; i++)
                {
                    int bone = mi.Skin.GetBindBone(i);
                    if (bone < 0) bone = skel.FindBone(mi.Skin.GetBindName(i));
                    bind[i] = bone >= 0 ? skel.GetBoneGlobalPose(bone) * mi.Skin.GetBindPose(i) : Transform3D.Identity;
                }
            }
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var arr = mi.Mesh.SurfaceGetArrays(s);
                var verts = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                int[] bones = bind != null ? arr[(int)Mesh.ArrayType.Bones].AsInt32Array() : null;
                float[] wts = bind != null ? arr[(int)Mesh.ArrayType.Weights].AsFloat32Array() : null;
                int per = bones != null && verts.Length > 0 ? bones.Length / verts.Length : 0;
                for (int v = 0; v < verts.Length; v += 3)
                {
                    Vector3 p = verts[v];
                    if (per > 0)
                    {
                        Vector3 acc = Vector3.Zero; float wsum = 0f;
                        for (int k = 0; k < per; k++)
                        {
                            float w = wts[v * per + k];
                            if (w <= 0f) continue;
                            int b = bones[v * per + k];
                            if (b < 0 || b >= bind.Length) continue;
                            acc += (bind[b] * p) * w; wsum += w;
                        }
                        if (wsum > 0f) p = acc / wsum;
                    }
                    float y = (xf * p).Y - ground;
                    if (y < low) low = y;
                    if (y > high) high = y;
                }
            }
        }
        return low == float.MaxValue ? (0, 0) : (low, high);
    }

    private static IEnumerable<MeshInstance3D> FindMeshes(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is MeshInstance3D mi) yield return mi;
            foreach (var d in FindMeshes(c)) yield return d;
        }
    }

    private static string Clip(Monster m)
    {
        if (m?.Model?.Anim == null || !GodotObject.IsInstanceValid(m.Model.Anim)) return "-";
        string c = m.Model.Anim.CurrentAnimation.ToString();
        return string.IsNullOrEmpty(c) ? "-" : m.Model.OwnAnimations ? CharModel.CreatureClipFor(c) : c;
    }

    private IEnumerator CreatureSuite(string[] parts)
    {
        bool shots = parts.Contains("shots");
        var meta = ReadBeastMeta();
        var only = parts.Skip(1).Where(x => x != "shots" && x.Length > 0).SelectMany(x => x.Split(',')).ToHashSet();
        var byModel = Bestiary.Types.Where(t => t.Model != null && meta.ContainsKey(t.Model)).GroupBy(t => t.Model)
            .Select(g => g.OrderBy(t => t.Build == "base" ? 0 : 1).First()).ToList();
        if (only.Count > 0) byModel = byModel.Where(t => only.Contains(t.Id) || only.Contains(t.Model)).ToList();
        _section = "creatures";
        Log.Info("suite", $"creatures: {byModel.Count} models (meta {meta.Count})");
        string region = null;
        foreach (var t in byModel.OrderBy(t => t.Regions.FirstOrDefault()).ThenBy(t => t.Id))
        {
            string want = t.Regions.FirstOrDefault() ?? "wastes";
            if (region == null) yield return StartHero(ClassId.Lampknight, 30, want, gear: true, god: true);
            else if (region != want) yield return TravelTo(want);
            region = want;
            var p = G.Player;
            ClearField();
            Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 8);
            p.Teleport(arena); p.FaceDir(Vector2.Down); p.Life = p.MaxLife;
            yield return 0.3f;
            var def = MonsterDb.Get(t.Id);
            if (def == null) { Fail($"creature {t.Id}: no definition"); continue; }
            var mt = meta[t.Model];
            int errs = Log.ErrorCount;
            Vector2 at = World.Grid.NearestWalk(arena + new Vector2(5f, 1.5f));        // beside the hero, in frame, outside its notice
            var m = Monster.Spawn(def, 30, MonTier.Normal, at, Faction.Monster);
            m.NoLoot = true;
            m.FaceDir(arena - at);
            var mod = m.Model;
            var seen = new HashSet<string>();
            double frozen0 = AnimCheck.MonFrozen;
            // spawn → idle (not aggro yet)
            for (int i = 0; i < 15; i++) { seen.Add(Clip(m)); yield return 0.1f; }
            float scl = mod?.Scale.Y ?? 1f;
            // idle extent: median of ten samples over a second (a monster that noticed the hero and swooped or leapt in one
            // of them does not decide the row)
            var lows = new List<float>(); var highs = new List<float>();
            for (int i = 0; i < 10; i++) { var e = PosedExtent(m); lows.Add(e.low); highs.Add(e.high); seen.Add(Clip(m)); yield return 0.1f; }
            lows.Sort(); highs.Sort();
            (float low, float high) idle = (lows[5], highs[5]);
            if (shots) yield return Snap($"{t.Model}_idle", 22f);
            // awake and walking: the hero steps 9 m away, twice
            m.AiWakeNow();
            float walkLow = float.MaxValue, walkHigh = float.MinValue;
            for (int i = 0; i < 30 && GodotObject.IsInstanceValid(m) && !m.Dead; i++)
            {
                if (i % 12 == 0) { var away = World.Grid.NearestWalk(m.Pos2 + (p.Pos2 - m.Pos2).Normalized() * 9f); p.Teleport(away); }
                seen.Add(Clip(m));
                if (Clip(m) is "Walk" or "Run") { var e = PosedExtent(m); walkLow = Mathf.Min(walkLow, e.low); walkHigh = Mathf.Max(walkHigh, e.high); }
                p.Life = p.MaxLife;
                yield return 0.1f;
            }
            if (!GodotObject.IsInstanceValid(m) || m.Dead)
            {
                // a pack-mule's connecting charge sets off its own kegs (by design): a fresh one for the hit and death checks
                Log.Info("suite", $"creature {t.Id}: died while walking (own charge / burst); a fresh one for the hit test");
                m = Monster.Spawn(def, 30, MonTier.Normal, at, Faction.Monster);
                m.NoLoot = true; mod = m.Model;
                yield return 2.5f;
            }
            // the hit reaction: a blow big enough to flinch (life restored after); the flinch rule skips monsters that are
            // mid-attack, so after three tries the clip is asked for directly (proves the file's Hit clip is reachable)
            string hitClip = "-", hitHow = "flinch";
            bool hitOk = false;
            for (int tries = 0; tries < 3 && !hitOk && GodotObject.IsInstanceValid(m) && !m.Dead; tries++)
            {
                yield return 1.0f;
                float life = m.Life;
                m.TakeHit(new Hit { Source = p, Amount = Mathf.Min(m.MaxLife * 0.5f, m.MaxLife * 0.06f * Mathf.Max(1f, def.Poise)), Dir = (m.Pos2 - p.Pos2).Normalized() });
                for (int i = 0; i < 4; i++) { yield return 0.05f; string c = Clip(m); seen.Add(c); if (c == "Hit") { hitOk = true; hitClip = c; } else if (hitClip == "-") hitClip = c; }
                if (GodotObject.IsInstanceValid(m) && !m.Dead) m.Life = Mathf.Max(life, m.MaxLife * 0.5f);
            }
            // death
            Vector3 deadAt = m.GlobalPosition;
            m.TakeHit(new Hit { Source = p, Amount = m.MaxLife * 20f, Dir = (m.Pos2 - p.Pos2).Normalized() });
            string deathClip = "-";
            for (int i = 0; i < 4; i++) { yield return 0.1f; if (GodotObject.IsInstanceValid(m)) { deathClip = Clip(m); seen.Add(deathClip); } }
            float gone = 0.4f;
            while (GodotObject.IsInstanceValid(m) && gone < 9f) { gone += 0.25f; yield return 0.25f; }
            // the corpse pose: the same model alone (no AI), its Death clip played to the end
            (float low, float high) dead = (0f, 0f);
            var corpse = CharModel.Create(t.Model);
            World.Root.AddChild(corpse);
            corpse.GlobalPosition = deadAt;
            corpse.Scale = Vector3.One * scl;
            yield return 0.2f;
            if (!hitOk && corpse.Anim != null)
            {
                // the AI never let it flinch (turrets, swarms, burrowers re-assert their own clip at once): does the file's Hit
                // clip play when asked for, on the AI-less copy?
                hitHow = "copy only (no flinch in play)";
                corpse.Play("Hit_Chest", 1f, 0f, true);
                yield return 0.1f;
                string c = corpse.Anim.CurrentAnimation.ToString();
                hitClip = string.IsNullOrEmpty(c) ? "-" : corpse.OwnAnimations ? CharModel.CreatureClipFor(c) : c;
                hitOk = hitClip == "Hit";
                yield return 0.5f;
            }
            corpse.Play("Death01", 1f, 0f, true);
            float len = corpse.Anim != null ? (float)corpse.Anim.CurrentAnimationLength : 1.5f;
            yield return Mathf.Clamp(len, 0.5f, 4f) + 0.25f;
            dead = PosedExtent(corpse, corpse.GlobalPosition.Y);
            if (shots) yield return Snap($"{t.Model}_dead", 22f);
            corpse.QueueFree();
            yield return null;

            // judgement
            float wantH = mt.Height * scl;          // generator height at scale 1 × the game's model scale
            bool hover = mt.Hover || mt.FlyHeight > 0f;
            bool buried = mt.Buried > 0f;
            // the generator measures from the ground up (a buried column only above the ground)
            float ratio = wantH > 0.05f ? (buried ? idle.high : idle.high - Mathf.Min(0f, idle.low)) / wantH : 0f;
            double frozen = AnimCheck.MonFrozen - frozen0;
            var bad = new List<string>();
            bool clipsOk = mod != null && mod.OwnAnimations && !mod.IsStandIn;
            if (!clipsOk) bad.Add(mod == null ? "no model" : mod.IsStandIn ? $"stand-in {mod.FileId}" : "no own clips");
            if (ratio < 0.8f || ratio > 1.25f) bad.Add($"height {idle.high:0.00} m vs meta {wantH:0.00} m (×{ratio:0.00})");
            if (!hover && !buried && (idle.low < -0.10f * scl || idle.low > 0.12f * scl)) bad.Add($"idle lowest point {idle.low:0.00} m");
            if (hover && idle.low < 0.03f) bad.Add($"hover creature touches the ground in Idle ({idle.low:0.00} m)");
            if (!hover && !buried && walkLow != float.MaxValue && (walkLow < -0.15f * scl || walkLow > 0.25f * scl)) bad.Add($"walking lowest point {walkLow:0.00} m");
            if (!hitOk) bad.Add($"Hit clip not reachable (clip {hitClip})");
            if (frozen > 0.5) bad.Add($"legs frozen {frozen:0.0} s while moving ({AnimCheck.MonSlideClip})");
            if (deathClip != "Death") bad.Add($"death clip {deathClip}");
            if (!buried && dead.low < -0.25f * scl) bad.Add($"corpse sinks ({dead.low:0.00} m)");
            if (!buried && dead.low > 0.35f * scl) bad.Add($"corpse floats ({dead.low:0.00} m)");
            if (gone >= 9f) bad.Add("body not removed after 9 s");
            if (Log.ErrorCount != errs) bad.Add($"{Log.ErrorCount - errs} errors");
            Log.Info("suite", $"creature {t.Model} ({t.Id}, rig {mt.Rig}{(hover ? ", hover" : "")}{(buried ? ", buried" : "")}) scale={scl:0.00} file={mod?.FileId} own={mod?.OwnAnimations} " +
                $"idle low={idle.low:0.00} high={idle.high:0.00} metaH={wantH:0.00} ratio={ratio:0.00} walk low={(walkLow == float.MaxValue ? "-" : walkLow.ToString("0.00"))} " +
                $"hit={hitClip}({hitHow}) frozen={frozen:0.0}s death={deathClip} dead low={dead.low:0.00} high={dead.high:0.00} removed={(gone < 9f ? gone.ToString("0.0") + "s" : "no")} clips=[{string.Join(",", seen.Where(s => s != "-"))}]");
            Check(bad.Count == 0, $"creature {t.Model}", string.Join("; ", bad));
        }
        ClearField();
        G.HarnessFov(0f);
        Check(Log.ErrorCount == 0, "creatures: no errors in the log", $"errors={Log.ErrorCount}");
    }
}
