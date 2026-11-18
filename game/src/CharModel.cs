using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// A rigged character: loads res://assets/chars/&lt;id&gt;.glb (mesh + shared humanoid rig, no animations), attaches the
/// shared animation library, converts materials to the character shader, and exposes gameplay visuals
/// (animation playback, weapon tier, tint, hit flash, freeze, ash dissolve).
/// </summary>
public partial class CharModel : Node3D
{
    private static readonly Dictionary<string, PackedScene> Scenes = new();
    private static readonly Dictionary<string, AnimationLibrary> Libraries = new();   // keyed by skeleton path
    private static readonly Dictionary<ulong, Material> Converted = new();
    private static AnimationLibrary _baseLib;
    private static string _baseSkelPath;
    private static Shader _charShader, _spectralShader;
    private static Texture2D _noise;
    public static readonly HashSet<string> Missing = new();
    /// <summary>Measurement only (--perf-off=anim): new models do not animate.</summary>
    public static bool NoAnimation;

    public string ModelId { get; private set; }
    public Skeleton3D Skeleton { get; private set; }
    public AnimationPlayer Anim { get; private set; }
    private readonly List<MeshInstance3D> _meshes = new();
    private readonly List<MeshInstance3D> _weapons = new();
    private Node3D _inst;
    private string _current = "";
    private float _flash, _dissolve = 0f, _frozen;
    private bool _dissolving;
    private Color _tint = Colors.White, _rim = Colors.Black;
    public bool IsPlaceholder { get; private set; }
    public float Height = 1.8f;

    public static Texture2D Noise
    {
        get
        {
            if (_noise == null)
            {
                var fn = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.02f, FractalOctaves = 4 };
                _noise = new NoiseTexture2D { Noise = fn, Width = 256, Height = 256, Seamless = true };
            }
            return _noise;
        }
    }

    public static CharModel Create(string modelId)
    {
        var m = new CharModel { ModelId = modelId, Name = "Model" };
        m.Build();
        return m;
    }

    private static readonly Dictionary<string, int> SceneEpoch = new();
    private static int _epoch;

    /// <summary>
    /// Called by LevelBuilder when a level of another theme is built, so video memory stays bounded: models nobody
    /// asked for during the last two levels are forgotten (live instances keep their own meshes), and converted
    /// materials that no mesh uses any more are dropped, which releases their 2K maps.
    /// </summary>
    public static void TrimCache()
    {
        _epoch++;
        var old = new List<string>();
        foreach (var kv in SceneEpoch) if (kv.Value < _epoch - 1) old.Add(kv.Key);
        foreach (var id in old) { Scenes.Remove(id); SceneEpoch.Remove(id); }
        var dead = new List<ulong>();
        foreach (var kv in Converted)
            if (kv.Value == null || !GodotObject.IsInstanceValid(kv.Value) || kv.Value.GetReferenceCount() <= 1) dead.Add(kv.Key);
        foreach (var k in dead) Converted.Remove(k);
        Log.Debug("model", $"cache trim: {old.Count} models and {dead.Count} materials released, {Scenes.Count} models and {Converted.Count} materials kept");
    }

    private static PackedScene LoadScene(string id)
    {
        SceneEpoch[id] = _epoch;
        if (Scenes.TryGetValue(id, out var s)) return s;
        string path = $"res://assets/chars/{id}.glb";
        if (ResourceLoader.Exists(path)) s = GD.Load<PackedScene>(path);
        if (s == null && Missing.Add(id))
        {
            // bestiary creatures that are still being modelled are expected (the boot summary lists them); anything else is worth a warning
            if (Bestiary.TypeOfModel(id) != null) Log.Debug("model", $"model '{id}' is not built yet, using a stand-in from the bestiary");
            else Log.Warn("model", $"missing character model '{id}', using a stand-in");
        }
        Scenes[id] = s;
        return s;
    }

    /// <summary>True when the shared humanoid library has a clip of this name (the eight creature clip names included).</summary>
    public static bool HasClip(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        EnsureBaseLibrary();
        return _baseLib != null && _baseLib.HasAnimation(name);
    }

    // Stand-ins used while a model has not been produced yet (or failed to load): the closest existing silhouette.
    private static readonly Dictionary<string, string[]> StandIns = new()
    {
        ["hero_hunter"] = new[] { "hero_pactbinder", "hero_emberblade" }, ["hero_wildsoul"] = new[] { "hero_soulbinder", "hero_emberblade" },
        ["hero_forgebody"] = new[] { "hero_emberblade" }, ["hero_lampknight"] = new[] { "hero_emberblade" },
        ["hero_astromancer"] = new[] { "hero_pactbinder", "hero_emberblade" }, ["hero_soulbinder"] = new[] { "hero_pactbinder", "hero_emberblade" },
        ["hero_pactbinder"] = new[] { "hero_soulbinder", "hero_emberblade" },
        ["form_bear"] = new[] { "mon_brute" }, ["form_wolf"] = new[] { "mon_fiend", "mon_husk" },
        ["npc_elder"] = new[] { "npc_villager_m" }, ["npc_smith"] = new[] { "npc_villager_m" },
        ["npc_merchant"] = new[] { "npc_villager_f" }, ["npc_occultist"] = new[] { "npc_villager_f", "mon_cult_caster" },
        ["mon_frostbound"] = new[] { "mon_skeleton" }, ["mon_rime_stalker"] = new[] { "mon_fiend", "mon_husk" }, ["mon_sand_revenant"] = new[] { "mon_skeleton", "mon_cultist" },
        ["mon_mire_hag"] = new[] { "mon_cult_caster" }, ["mon_rotspawn"] = new[] { "mon_brute", "mon_husk_bloated" }, ["mon_brass_sentinel"] = new[] { "mon_knight" },
        ["mon_choir"] = new[] { "mon_wraith", "mon_cult_caster" }, ["mon_wraith"] = new[] { "mon_cult_caster" }, ["mon_skeleton_thrower"] = new[] { "mon_skeleton" },
        ["boss_gravedigger"] = new[] { "mon_brute", "mon_husk_bloated" }, ["boss_rimelord"] = new[] { "mon_knight" }, ["boss_bishop"] = new[] { "mon_cult_caster" },
        ["boss_mire_mother"] = new[] { "mon_cult_caster" }, ["boss_sand_tyrant"] = new[] { "mon_knight" }, ["boss_grand_hush"] = new[] { "mon_cult_caster" },
        ["boss_osaros"] = new[] { "mon_knight" },
    };
    public bool IsStandIn { get; private set; }
    /// <summary>The file this model was really built from (its own id, or the stand-in's).</summary>
    public string FileId { get; private set; }

    private PackedScene Try(string id)
    {
        var s = LoadScene(id);
        if (s != null) FileId = id;
        return s;
    }

    private PackedScene Resolve()
    {
        var scene = Try(ModelId);
        if (scene != null) return scene;
        IsStandIn = true;
        // monsters: the bestiary says what a missing model is drawn with (its variant base, the closest built member of its
        // family, a creature of the same rig, a generic for its body plan). Nothing here changes when the real file arrives:
        // after an import the first line of this method finds it.
        foreach (var a in Bestiary.StandInsFor(ModelId))
        {
            if (a.StartsWith("prim_", StringComparison.Ordinal)) { FileId = a; return null; }
            scene = Try(a);
            if (scene != null) return scene;
        }
        if (StandIns.TryGetValue(ModelId, out var alts))
            foreach (var a in alts) { scene = Try(a); if (scene != null) return scene; }
        string generic = ModelId.StartsWith("hero_") ? "hero_emberblade" : ModelId.StartsWith("npc_") ? "npc_villager_m" : "mon_skeleton";
        scene = Try(generic) ?? Try("mon_skeleton") ?? Try("hero_emberblade");
        if (scene != null) return scene;
        IsStandIn = false; IsPlaceholder = true;
        return Try("placeholder");
    }

    private void Build()
    {
        if (ModelId != null && ModelId.StartsWith("prim_", StringComparison.Ordinal)) { FileId = ModelId; BuildPrimitive(ModelId); return; }
        var scene = Resolve();
        if (scene == null && FileId != null && FileId.StartsWith("prim_", StringComparison.Ordinal)) { BuildPrimitive(FileId); return; }
        if (scene == null) { Log.Error("model", $"no model available for '{ModelId}' and no stand-in"); return; }
        _inst = scene.Instantiate<Node3D>();
        AddChild(_inst);
        Skeleton = FindSkeleton(_inst);
        // a creature on its own rig brings its own clips (Idle, Walk, Run, Attack1, Attack2, Hit, Death, Spawn)
        if (!IsPlaceholder && Bestiary.Live) _clips = OwnClips(FileId, _inst, Skeleton);
        foreach (var mi in Walk(_inst).OfType<MeshInstance3D>())
        {
            _meshes.Add(mi);
            if (mi.Name.ToString().StartsWith("wpn_", StringComparison.OrdinalIgnoreCase)) _weapons.Add(mi);
            ConvertMaterials(mi);
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            mi.ExtraCullMargin = 1.5f;

        }
        // any animation player that came with the file is replaced by the shared library
        foreach (var ap in Walk(_inst).OfType<AnimationPlayer>().ToList()) { if (!IsPlaceholder) ap.QueueFree(); else ap.Stop(); }
        SetupAnimation();
        SetWeaponTier(1);

        var aabb = new Aabb();
        bool first = true;
        foreach (var mi in _meshes) { var a = mi.GetAabb(); if (first) { aabb = a; first = false; } else aabb = aabb.Merge(a); }
        if (!first && aabb.Size.Y > 0.5f) Height = aabb.Size.Y;
    }

    private static IEnumerable<Node> Walk(Node n)
    {
        yield return n;
        foreach (var c in n.GetChildren()) foreach (var d in Walk(c)) yield return d;
    }

    private static Skeleton3D FindSkeleton(Node root) => Walk(root).OfType<Skeleton3D>().FirstOrDefault();

    // ───────────── animation ─────────────

    private static void EnsureBaseLibrary()
    {
        if (_baseLib != null) return;
        _baseLib = new AnimationLibrary();
        foreach (var file in new[] { "anim_lib", "placeholder", "placeholder2" })
        {
            string path = $"res://assets/chars/{file}.glb";
            if (!ResourceLoader.Exists(path)) continue;
            var inst = GD.Load<PackedScene>(path).Instantiate();
            var ap = Walk(inst).OfType<AnimationPlayer>().FirstOrDefault();
            var sk = FindSkeleton(inst);
            if (ap != null && sk != null)
            {
                _baseSkelPath ??= inst.GetPathTo(sk).ToString();
                string thisPath = inst.GetPathTo(sk).ToString();
                int n = 0;
                foreach (var libName in ap.GetAnimationLibraryList())
                {
                    var lib = ap.GetAnimationLibrary(libName);
                    foreach (var an in lib.GetAnimationList())
                    {
                        if (_baseLib.HasAnimation(an)) continue;
                        var a = (Animation)lib.GetAnimation(an).Duplicate();
                        if (thisPath != _baseSkelPath) Retarget(a, thisPath, _baseSkelPath);
                        // Godot's importer marks clips named "..._Loop" as looping and may strip that suffix from the
                        // name ("Jog_Fwd_Loop" becomes "Jog_Fwd"). Keep the imported loop flag, and register every
                        // looping clip under its "_Loop" name as well, because the game asks for clips by that name.
                        string name = an.ToString();
                        // "Sword_Idle" is a held stance (hero idle, boss brace) that the library ships without the suffix
                        bool loops = a.LoopMode != Animation.LoopModeEnum.None || name.Contains("Loop", StringComparison.OrdinalIgnoreCase) || name == "Sword_Idle";
                        a.LoopMode = loops ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
                        _baseLib.AddAnimation(an, a);
                        if (loops && !name.EndsWith("_Loop", StringComparison.OrdinalIgnoreCase) && !_baseLib.HasAnimation(name + "_Loop"))
                            _baseLib.AddAnimation(name + "_Loop", a);
                        n++;
                    }
                }
                Log.Info("model", $"animation source {file}.glb: {n} animations, skeleton path '{thisPath}'");
            }
            inst.Free();
            if (file == "anim_lib" && _baseLib.GetAnimationList().Count > 60) break;   // the full library is present
        }
        if (_baseLib.GetAnimationList().Count == 0) Log.Error("model", "no animations found (anim_lib.glb / placeholder.glb missing)");
        else
        {
            // The eight clip names of creatures on their own rigs also work on the humanoid rig, so a beast that is still
            // drawn with a humanoid stand-in can be asked for "Attack2" or "Spawn" like the real model will be.
            if (Bestiary.Live)
                foreach (var (name, src) in CreatureOnHumanoid)
                    if (!_baseLib.HasAnimation(name) && _baseLib.HasAnimation(src)) _baseLib.AddAnimation(name, _baseLib.GetAnimation(src));
            Log.Info("model", "animations: " + string.Join(" ", _baseLib.GetAnimationList().Select(x => x.ToString()).OrderBy(x => x)));
            foreach (var need in new[] { "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Sword_Idle", "Zombie_Walk_Fwd_Loop", "Zombie_Idle_Loop", "Idle_Talking_Loop", "Roll", "Death01" })
                if (!_baseLib.HasAnimation(need)) Log.Error("model", $"animation '{need}' is missing from the shared library: characters will not animate when it is requested");
        }
    }

    private static void Retarget(Animation a, string from, string to)
    {
        for (int t = 0; t < a.GetTrackCount(); t++)
        {
            string p = a.TrackGetPath(t).ToString();
            if (p.StartsWith(from + ":")) a.TrackSetPath(t, to + p[from.Length..]);
        }
    }

    private void SetupAnimation()
    {
        EnsureBaseLibrary();
        if (Skeleton == null || _baseSkelPath == null)
        {
            // no rig, no animation: the model would stand in its bind pose for ever. Count it and say so once per model.
            Unanimated++;
            if (NoRig.Add(ModelId ?? "")) Log.Error("model", $"model '{ModelId}' has {(Skeleton == null ? "no skeleton" : "no animation library")}: it cannot be animated");
            return;
        }
        string skelPath = _inst.GetPathTo(Skeleton).ToString();
        AnimationLibrary lib;
        if (_clips != null) lib = _clips.Lib;                // its own clip set, every humanoid clip name aliased onto it
        else if (!Libraries.TryGetValue(skelPath, out lib))
        {
            if (skelPath == _baseSkelPath) lib = _baseLib;
            else
            {
                lib = new AnimationLibrary();
                foreach (var an in _baseLib.GetAnimationList())
                {
                    var a = (Animation)_baseLib.GetAnimation(an).Duplicate();
                    Retarget(a, _baseSkelPath, skelPath);
                    lib.AddAnimation(an, a);
                }
            }
            Libraries[skelPath] = lib;
        }
        Anim = new AnimationPlayer { Name = "Anim" };
        _inst.AddChild(Anim);
        Anim.RootNode = Anim.GetPathTo(_inst);
        Anim.AddAnimationLibrary("", lib);
        Anim.PlaybackDefaultBlendTime = 0.15;
        Play("Idle_Loop");
        if (NoAnimation) Anim.Active = false;
    }

    /// <summary>True while a clip is really advancing (player present, active, not paused or finished).</summary>
    public bool Playing => Anim != null && IsInstanceValid(Anim) && Anim.Active && Anim.IsPlaying();

    private int _legBone = -2;
    /// <summary>
    /// A number that changes whenever the pose of a leg changes. The animation self-check samples it while an actor
    /// moves: a clip that "plays" without moving the skeleton (wrong track paths, a frozen player) leaves it constant.
    /// </summary>
    public float LegSignature()
    {
        if (Skeleton == null) return 0f;
        if (_clips != null)
        {
            // no thighs to find on a beetle, a serpent or a hanging bell: any bone that turns or travels counts
            float sum = 0f;
            int n = Skeleton.GetBoneCount();
            for (int i = 0; i < n; i++)
            {
                var br = Skeleton.GetBonePoseRotation(i); var bp = Skeleton.GetBonePosePosition(i);
                sum += (br.X * 3f + br.Y * 5f + br.Z * 7f + br.W + bp.X * 2f + bp.Y * 3f + bp.Z * 5f) * (1 + i % 5);
            }
            return sum;
        }
        if (_legBone == -2)
        {
            _legBone = -1;
            foreach (var name in new[] { "thigh_l", "calf_l", "DEF-thigh.L", "LeftUpLeg" })
            { int i = Skeleton.FindBone(name); if (i >= 0) { _legBone = i; break; } }
            if (_legBone < 0)
                for (int i = 0; i < Skeleton.GetBoneCount(); i++)
                {
                    string n = Skeleton.GetBoneName(i).ToLowerInvariant();
                    if (n.Contains("thigh") || n.Contains("upleg") || n.Contains("upper_leg")) { _legBone = i; break; }
                }
        }
        if (_legBone < 0) return 0f;
        var q = Skeleton.GetBonePoseRotation(_legBone);
        return q.X * 3f + q.Y * 5f + q.Z * 7f + q.W;
    }

    /// <summary>
    /// Ground speed (m/s) a locomotion clip covers at playback speed 1 on a model of scale 1, measured from the foot
    /// travel of each clip (--suite=anims prints the measurement and fails when this table drifts from the assets).
    /// </summary>
    public static readonly Dictionary<string, float> Stride = new()
    {
        ["Walk_Loop"] = 0.95f, ["Walk_Formal_Loop"] = 0.95f, ["Walk_Carry_Loop"] = 0.64f, ["Jog_Fwd_Loop"] = 5.3f, ["Sprint_Loop"] = 8.0f,
        ["Zombie_Walk_Fwd_Loop"] = 0.95f, ["Crouch_Fwd_Loop"] = 0.8f,
    };

    public static bool IsLocomotion(string clip) => clip != null && Stride.ContainsKey(clip);

    /// <summary>
    /// Plays the locomotion clip that fits a ground speed: of the candidates, the one whose stride needs the least
    /// stretching, played at speed ÷ (stride × model scale) so the feet do not slide. Returns the clip chosen.
    /// </summary>
    public string Locomote(float speed, float blend, params string[] clips)
    {
        float scale = Mathf.Max(0.05f, Scale.X);
        string best = null; float bestCost = float.MaxValue, bestRate = 1f;
        foreach (var c in clips)
        {
            if (c == null || !Stride.TryGetValue(c, out float stride)) continue;
            float rate = Mathf.Clamp(speed / (stride * scale), MinMoveRate, MaxMoveRate);
            // first the feet must match the ground (what is left after clamping is sliding), then prefer the natural tempo
            float slide = Mathf.Abs(Mathf.Log(Mathf.Max(speed, 0.01f) / (stride * scale * rate)));
            float cost = slide * 3f + Mathf.Abs(Mathf.Log(rate));
            if (c == _current) cost -= 0.12f;                    // do not flip between two gaits at the boundary
            if (cost < bestCost) { bestCost = cost; best = c; bestRate = rate; }
        }
        if (best == null)
        {
            // an unknown clip: play the first one that exists at a plain rate rather than nothing
            foreach (var c in clips) if (c != null) { best = c; break; }
            best ??= "Walk_Loop";
            bestRate = 1f;
        }
        Play(best, Mathf.Clamp(bestRate, MinMoveRate, MaxMoveRate), blend);
        return best;
    }

    public const float MinMoveRate = 0.5f, MaxMoveRate = 2.4f;

    /// <summary>
    /// When each attack clip strikes: the moment its fastest hand or foot passes, in seconds at rate 1 (measured;
    /// --suite=anims re-measures and fails when this table drifts). <see cref="PlayAttack"/> lines the clip up with the
    /// moment the blow lands, whatever contact time the skill or monster data asks for.
    /// </summary>
    public static readonly Dictionary<string, float> Strike = new()
    {
        ["Sword_Regular_A"] = 0.25f, ["Sword_Regular_B"] = 0.27f, ["Sword_Regular_C"] = 0.65f, ["Sword_Regular_Combo"] = 0.25f,
        ["Sword_Attack"] = 0.38f, ["Sword_Heavy_Combo"] = 0.38f, ["Melee_Hook"] = 0.25f, ["Punch_Cross"] = 0.18f, ["Punch_Jab"] = 0.15f,
        ["OverhandThrow"] = 0.28f, ["Zombie_Scratch"] = 0.57f, ["Shield_OneShot"] = 0.05f,
    };

    public static float StrikeOr(string clip, float fallback) => clip != null && Strike.TryGetValue(clip, out float t) ? t : fallback;

    /// <summary>Where in the clip the blow lands after <see cref="PlayAttack"/> has aligned it (for the audit).</summary>
    public static float ClipTimeAtContact(string clip, float speed, float contactIn)
    {
        float atContact = contactIn * speed;
        if (!Strike.TryGetValue(clip ?? "", out float st) || contactIn <= 0.01f || speed <= 0.01f) return atContact;
        if (st > atContact + 0.04f) return st;
        if (st < atContact - 0.04f) return Mathf.Max(st / contactIn, speed * 0.35f) * contactIn;
        return atContact;
    }

    private float _restoreIn, _restoreRate;

    /// <summary>
    /// Plays an attack so that its swing meets the blow, which lands <paramref name="contactIn"/> seconds from now: a clip
    /// that strikes late starts inside its wind-up; one that strikes early has its wind-up stretched and returns to
    /// full speed for the follow-through.
    /// </summary>
    public void PlayAttack(string anim, float speed, float contactIn, float blend)
    {
        Play(anim, speed, blend, true);
        float st = 0f;
        // a creature with its own clips strikes when its own clip does (measured when the model was first loaded)
        bool known = _clips != null ? _clips.Strike.TryGetValue(anim, out st) : Strike.TryGetValue(anim, out st);
        if (Anim == null || _current != anim || !known || contactIn <= 0.01f || speed <= 0.01f) return;
        float atContact = contactIn * speed;          // clip seconds that will have played when the blow lands
        if (st > atContact + 0.04f) Anim.Seek(Mathf.Min(st - atContact, Len(anim) - 0.05f), true);
        else if (st < atContact - 0.04f)
        {
            Anim.SpeedScale = Mathf.Max(st / contactIn, speed * 0.35f);
            _restoreIn = contactIn; _restoreRate = speed;
            SetProcess(true);
        }
    }

    public bool Has(string anim) => Anim != null && Anim.HasAnimation(anim);
    public float Len(string anim) => Has(anim) ? (float)Anim.GetAnimation(anim).Length : 0.6f;
    public string Current => _current;

    private static readonly HashSet<string> MissingAnims = new();
    private static readonly HashSet<string> NoRig = new();
    /// <summary>How many models were built without a working animation player (see AnimCheck).</summary>
    public static int Unanimated;

    // ───────────── diagnostics (animation audit) ─────────────

    /// <summary>How many tracks of a clip reach a bone of this model's skeleton (ok, total).</summary>
    public (int ok, int total) TrackCoverage(string clip)
    {
        if (Anim == null || Skeleton == null || !Anim.HasAnimation(clip)) return (0, 0);
        var a = Anim.GetAnimation(clip);
        var root = Anim.GetNodeOrNull(Anim.RootNode);
        int ok = 0, total = a.GetTrackCount();
        for (int t = 0; t < total; t++)
        {
            string path = a.TrackGetPath(t).ToString();
            int colon = path.LastIndexOf(':');
            if (colon <= 0) continue;
            var node = root?.GetNodeOrNull(path[..colon]);
            if (node is Skeleton3D sk && sk.FindBone(path[(colon + 1)..]) >= 0) ok++;
        }
        return (ok, total);
    }

    /// <summary>How far the current pose is from the bind pose (0 = the model stands in its rest / T-pose).</summary>
    public float PoseOffRest()
    {
        if (Skeleton == null) return 0f;
        float sum = 0f;
        for (int i = 0; i < Skeleton.GetBoneCount(); i++)
        {
            var rest = Skeleton.GetBoneRest(i).Basis.GetRotationQuaternion();
            var q = Skeleton.GetBonePoseRotation(i);
            sum += 1f - Mathf.Abs(rest.Dot(q));
        }
        return sum;
    }

    /// <summary>A number that changes whenever any bone moves: two readings some time apart tell a living pose from a frozen one.</summary>
    public float PoseSignature()
    {
        if (Skeleton == null) return 0f;
        float sum = 0f;
        for (int i = 0; i < Skeleton.GetBoneCount(); i++)
        {
            var q = Skeleton.GetBonePoseRotation(i);
            sum += (q.X * 3f + q.Y * 5f + q.Z * 7f + q.W) * (1 + i % 5);
        }
        return sum;
    }

    /// <summary>Position of a bone in the model's own space (y up, the model looks along +Z).</summary>
    public Vector3 BoneInModel(int bone)
    {
        if (Skeleton == null || bone < 0) return Vector3.Zero;
        return GlobalTransform.AffineInverse() * (Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(bone).Origin);
    }

    public int FindBoneLike(params string[] parts)
    {
        if (Skeleton == null) return -1;
        for (int i = 0; i < Skeleton.GetBoneCount(); i++)
        {
            string n = Skeleton.GetBoneName(i);
            bool all = true;
            foreach (var p in parts) if (!n.Contains(p, StringComparison.OrdinalIgnoreCase)) { all = false; break; }
            if (all) return i;
        }
        return -1;
    }

    /// <summary>
    /// The rate the current clip was asked to play at. For a creature with its own clips the player's real rate differs
    /// by the clip's stride factor (see <see cref="ClipSet.RateMul"/>): "Jog_Fwd_Loop at 1.0" always means "feet covering
    /// the humanoid jog's 5.3 m/s per unit of scale", whatever the creature's own run cycle covers.
    /// </summary>
    public float PlayRate => Anim != null ? (float)Anim.SpeedScale / _rateMul : 0f;
    private float _rateMul = 1f;

    public void Play(string anim, float speed = 1f, float blend = 0.15f, bool restart = false)
    {
        if (Anim == null) return;
        if (_restoreIn > 0f && (restart || anim != _current)) _restoreIn = 0f;
        if (!Anim.HasAnimation(anim))
        {
            // a silent fallback hid a real bug once (every "_Loop" clip was missing): always say which clip was asked for
            if (MissingAnims.Add(anim)) Log.Error("model", $"animation '{anim}' requested by '{ModelId}' does not exist; falling back to Idle_Loop");
            anim = "Idle_Loop";
        }
        if (!Anim.HasAnimation(anim)) return;
        float mul = _clips != null && _clips.RateMul.TryGetValue(anim, out float k) ? k : 1f;
        if (_current == anim && !restart && Anim.IsPlaying()) { _rateMul = mul; Anim.SpeedScale = speed * mul; return; }
        _paused = false;
        _current = anim;
        _rateMul = mul;
        Anim.SpeedScale = speed * mul;
        if (restart) Anim.Stop(true);
        Anim.Play(anim, blend);
    }

    /// <summary>Shows one frame of an animation and holds it there (a skeleton lying in the ash until it is woken).</summary>
    public void Hold(string anim, float time)
    {
        if (Anim == null || !Anim.HasAnimation(anim)) return;
        _current = anim;
        _rateMul = 1f;
        Anim.SpeedScale = 1f;
        Anim.Play(anim, 0);
        Anim.Seek(time, true);
        Anim.Pause();
        _paused = true; _resume = false;
    }

    private bool _paused, _resume;

    /// <summary>
    /// Freezes the pose (a frozen or stunned monster) or lets it run again. Un-pausing only resumes what Pause froze: it
    /// used to call Play() whenever nothing was playing, which restarted every finished one-shot — a monster began its
    /// attack swing a second time while it recovered from the first.
    /// </summary>
    public void Pause(bool paused)
    {
        if (Anim == null) return;
        if (paused) { if (!_paused) { _resume = Anim.IsPlaying(); Anim.Pause(); _paused = true; } }
        else if (_paused) { _paused = false; if (_resume && _current != "") Anim.Play(); }
    }

    // ───────────── materials ─────────────

    private static void ConvertMaterials(MeshInstance3D mi)
    {
        if (mi.Mesh == null) return;
        _charShader ??= GD.Load<Shader>("res://shaders/char.gdshader");
        _spectralShader ??= GD.Load<Shader>("res://shaders/spectral.gdshader");
        for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
        {
            var src = mi.GetActiveMaterial(s);
            if (src == null) continue;
            if (!Converted.TryGetValue(src.GetInstanceId(), out var conv))
            {
                conv = Convert(src);
                Converted[src.GetInstanceId()] = conv;
            }
            mi.SetSurfaceOverrideMaterial(s, conv);
        }
    }

    private static Material Convert(Material src)
    {
        string name = (src.ResourceName ?? "").ToLowerInvariant();
        if (src is not BaseMaterial3D b) return src;
        if (name.StartsWith("spectral"))
        {
            var sm = new ShaderMaterial { Shader = _spectralShader, ResourceName = name };
            var c = b.EmissionEnabled ? b.Emission : b.AlbedoColor;
            sm.SetShaderParameter("color", new Color(c.R * 0.6f + 0.25f, c.G * 0.6f + 0.35f, c.B * 0.6f + 0.4f));
            sm.SetShaderParameter("noise_tex", Noise);
            // the model's own surface shows through as lighter and denser ghost matter (face, folds, mail)
            if (b.AlbedoTexture != null) { sm.SetShaderParameter("albedo_tex", b.AlbedoTexture); }      // "detail" stays 0 until the look is checked on screen
            return sm;
        }
        var m = new ShaderMaterial { Shader = _charShader, ResourceName = name };
        m.SetShaderParameter("albedo", b.AlbedoColor);
        if (b.AlbedoTexture != null) m.SetShaderParameter("albedo_tex", b.AlbedoTexture);
        if (b.NormalEnabled && b.NormalTexture != null) { m.SetShaderParameter("normal_tex", b.NormalTexture); m.SetShaderParameter("normal_scale", b.NormalScale); }
        m.SetShaderParameter("roughness", b.Roughness);
        m.SetShaderParameter("metallic", b.Metallic);
        var orm = b.RoughnessTexture ?? b.MetallicTexture ?? b.AOTexture;
        if (orm != null) m.SetShaderParameter("orm_tex", orm);
        // the red channel is occlusion only when the model says so; otherwise it is whatever the exporter left there
        bool hasAo = b.AOEnabled && b.AOTexture != null && (orm == null || b.AOTexture == orm);
        m.SetShaderParameter("ao_amount", hasAo ? 1f : 0f);
        m.SetShaderParameter("grade_tint", new Vector3(_gradeTint.R, _gradeTint.G, _gradeTint.B));
        m.SetShaderParameter("grade_desat", _gradeDesat);
        m.SetShaderParameter("scene_rim", new Vector3(_sceneRim.R, _sceneRim.G, _sceneRim.B));
        if (Log.Verbose)
            Log.Debug("model", $"material '{name}': albedoTex={(b.AlbedoTexture != null)} normal={(b.NormalEnabled && b.NormalTexture != null)}x{b.NormalScale:0.##} " +
                $"orm={(orm != null)} rough={b.Roughness:0.##} metal={b.Metallic:0.##} ao={hasAo} emission={(b.EmissionEnabled ? (b.EmissionTexture != null ? "texture" : "colour") : "none")}");
        bool emis = name.StartsWith("emis");
        if (b.EmissionEnabled || emis)
        {
            if (b.EmissionTexture != null) m.SetShaderParameter("emission_tex", b.EmissionTexture);
            var e = b.EmissionEnabled ? b.Emission : b.AlbedoColor;
            if (b.EmissionTexture != null && e.R + e.G + e.B > 2.9f) e = Colors.Black;   // white factor × texture: texture carries the colour
            m.SetShaderParameter("emission", new Vector3(e.R, e.G, e.B));
            float energy = b.EmissionEnabled ? Mathf.Max(b.EmissionEnergyMultiplier, 1f) : 1f;
            // an ember slit or an eye is a small light: bright enough to bloom a little, not enough to light the body
            m.SetShaderParameter("emission_energy", emis ? Mathf.Clamp(energy, 1f, 2.2f) : Mathf.Min(energy, 2f));
            m.SetShaderParameter("pulse", emis ? 0.5f : 0f);
        }
        if (b.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor) m.SetShaderParameter("alpha_cut", b.AlphaScissorThreshold);
        else if (b.Transparency != BaseMaterial3D.TransparencyEnum.Disabled) m.SetShaderParameter("alpha_cut", 0.4f);
        m.SetShaderParameter("noise_tex", Noise);
        return m;
    }

    private static Color _gradeTint = new(0.6f, 0.6f, 0.6f), _sceneRim = new(0f, 0f, 0f);
    private static float _gradeDesat = 0.2f;

    /// <summary>
    /// The grading characters share with the level they stand in (set by the level renderer for each level):
    /// <paramref name="tint"/> multiplies the albedo (theme tint × the factor that brings character textures into the
    /// value band of the levelled environment), <paramref name="desaturate"/> is a share of the theme's
    /// desaturation, <paramref name="rim"/> a thin cool edge away from the lens. Characters get no light of their
    /// own. Applies to all character materials, those already converted and those still to come.
    /// </summary>
    public static void SetSceneGrade(Color tint, float desaturate, Color rim)
    {
        _gradeTint = tint; _gradeDesat = Mathf.Clamp(desaturate, 0f, 1f); _sceneRim = rim;
        var t = new Vector3(tint.R, tint.G, tint.B);
        var r = new Vector3(rim.R, rim.G, rim.B);
        foreach (var mat in Converted.Values)
            if (mat is ShaderMaterial sm && sm.Shader == _charShader)
            {
                sm.SetShaderParameter("grade_tint", t);
                sm.SetShaderParameter("grade_desat", _gradeDesat);
                sm.SetShaderParameter("scene_rim", r);
            }
    }

    /// <summary>Test harness: every mesh of this model drawn in one flat colour (for actor masks), or back to normal with null.</summary>
    public void SetMask(Material mask) { foreach (var mi in _meshes) if (IsInstanceValid(mi)) mi.MaterialOverride = mask; }

    private void SetInst(StringName p, Variant v) { foreach (var mi in _meshes) mi.SetInstanceShaderParameter(p, v); }
    private static readonly StringName PTint = "tint", PFlash = "flash", PFlashColor = "flash_color", PRim = "rim_color", PDissolve = "dissolve", PFrozen = "frozen";

    public void SetTint(Color c) { _tint = c; SetInst(PTint, new Vector3(c.R, c.G, c.B)); }
    /// <summary>
    /// The gameplay rim: a thin fresnel edge (elite affix colour, a summon's teal). The hero's class colour is only
    /// hinted at — a figure that glows in its own colour looks pasted on the scene.
    /// </summary>
    public void SetRim(Color c)
    {
        _rim = c;
        float k = ModelId != null && ModelId.StartsWith("hero_", StringComparison.Ordinal) ? 0.25f : 0.8f;
        SetInst(PRim, new Vector3(c.R * k, c.G * k, c.B * k));
    }
    public void SetFrozen(float f) { if (Mathf.Abs(_frozen - f) > 0.01f) { _frozen = f; SetInst(PFrozen, f); } }

    public void Flash(Color c, float strength = 1f)
    {
        _flash = strength;
        SetProcess(true);
        SetInst(PFlashColor, new Vector3(c.R, c.G, c.B));
        SetInst(PFlash, _flash);
    }

    /// <summary>Starts the ash-dissolve; the caller frees the actor when <see cref="Dissolved"/> turns true.</summary>
    public void Dissolve() { _dissolving = true; SetProcess(true); foreach (var mi in _meshes) mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
    public bool Dissolved => _dissolve >= 1f;
    public float DissolveSpeed = 0.9f;

    public void SetWeaponTier(int tier)
    {
        if (_weapons.Count == 0) return;
        bool any = _weapons.Any(w => w.Name.ToString().StartsWith($"wpn_{tier}"));
        if (!any) tier = 1;
        foreach (var w in _weapons) w.Visible = w.Name.ToString().StartsWith($"wpn_{tier}");
    }

    public void ShowWeapons(bool show) { if (!show) foreach (var w in _weapons) w.Visible = false; }

    /// <summary>World position of a bone (e.g. the right hand) for effects; falls back to chest height.</summary>
    public Vector3 BonePos(string bone)
    {
        int i = BoneIndex(bone);
        if (i >= 0) return Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(i).Origin;
        // creatures without hands (and primitives): the middle of the body at its own size
        if (_clips != null || Skeleton == null) return GlobalPosition + Vector3.Up * (Height * Scale.Y * 0.6f);
        return GlobalPosition + Vector3.Up * 1.2f;
    }

    // The shared rig names its bones hand_r, foot_l, Head…; older code asked for Rigify names ("DEF-hand.R"), which never
    // matched, so every effect meant for the hand came out of the middle of the body. Both spellings resolve now.
    private static readonly Dictionary<string, string[]> BoneAlias = new()
    {
        ["hand_r"] = new[] { "hand_r", "DEF-hand.R", "RightHand", "hand.R" }, ["hand_l"] = new[] { "hand_l", "DEF-hand.L", "LeftHand", "hand.L" },
        ["head"] = new[] { "Head", "head", "DEF-spine.006", "DEF-head" }, ["foot_r"] = new[] { "foot_r", "DEF-foot.R", "RightFoot" },
        ["foot_l"] = new[] { "foot_l", "DEF-foot.L", "LeftFoot" }, ["chest"] = new[] { "spine_03", "DEF-spine.003", "Spine2" },
    };
    private readonly Dictionary<string, int> _boneIdx = new();
    private static readonly HashSet<string> MissingBones = new();

    public int BoneIndex(string bone)
    {
        if (Skeleton == null || bone == null) return -1;
        if (_boneIdx.TryGetValue(bone, out int idx)) return idx;
        idx = Skeleton.FindBone(bone);
        if (idx < 0 && _clips != null)
        {
            // a beast has no "hand_r": what it throws or spits leaves its head; anything else falls back to the body centre, silently
            if (bone is "hand_r" or "hand_l" or "head") idx = FindBoneLike("head");
            if (idx < 0 && bone is "hand_r" or "hand_l" or "head") idx = FindBoneLike("jaw");
            _boneIdx[bone] = idx;
            return idx;
        }
        if (idx < 0)
            foreach (var kv in BoneAlias)
            {
                if (kv.Key != bone && Array.IndexOf(kv.Value, bone) < 0) continue;
                foreach (var name in kv.Value) { idx = Skeleton.FindBone(name); if (idx >= 0) break; }
                break;
            }
        if (idx < 0 && MissingBones.Add(ModelId + ":" + bone)) Log.Warn("model", $"bone '{bone}' not found on '{ModelId}'; effects use the body centre instead");
        _boneIdx[bone] = idx;
        return idx;
    }

    // ───────────── creatures with their own clips (rigs B–F) ─────────────

    /// <summary>
    /// The clip set a creature file carries: Idle, Walk, Run, Attack1, Attack2, Hit, Death, Spawn (the importer strips a
    /// trailing "_Loop"). Every clip name of the shared humanoid library is registered as an alias of one of the eight, so
    /// all the code that asks for "Jog_Fwd_Loop", "Hit_Head", "LayToIdle" or "Death01" works on a hound or a beetle.
    /// </summary>
    private sealed class ClipSet
    {
        public AnimationLibrary Lib;
        /// <summary>Locomotion alias → factor on the playback rate, so that the alias covers the humanoid clip's stride (see PlayRate).</summary>
        public readonly Dictionary<string, float> RateMul = new();
        /// <summary>Attack alias → seconds into the clip when it strikes (fastest tip).</summary>
        public readonly Dictionary<string, float> Strike = new();
        public float WalkStride, RunStride;
        public string Note = "";
    }

    private static readonly string[] CreatureClips = { "Idle", "Walk", "Run", "Attack1", "Attack2", "Hit", "Death", "Spawn" };
    private static readonly (string name, string src)[] CreatureOnHumanoid =
    {
        ("Idle", "Idle_Loop"), ("Walk", "Walk_Loop"), ("Run", "Jog_Fwd_Loop"), ("Attack1", "Melee_Hook"), ("Attack2", "Sword_Attack"),
        ("Hit", "Hit_Chest"), ("Death", "Death01"), ("Spawn", "LayToIdle"),
    };
    // what stands in for a clip the file lacks, best first
    private static readonly Dictionary<string, string[]> ClipFallback = new()
    {
        ["Idle"] = new[] { "Walk", "Run" }, ["Walk"] = new[] { "Run", "Idle" }, ["Run"] = new[] { "Walk", "Idle" },
        ["Attack1"] = new[] { "Attack2", "Idle" }, ["Attack2"] = new[] { "Attack1", "Idle" }, ["Hit"] = new[] { "Idle" },
        ["Death"] = new[] { "Hit", "Idle" }, ["Spawn"] = new[] { "Idle" },
    };
    private static readonly Dictionary<string, ClipSet> OwnSets = new();     // by file id; null = the file carries no clip set

    /// <summary>True when this model animates with clips of its own (rigs B–F) instead of the shared humanoid library.</summary>
    public bool OwnAnimations => _clips != null;
    /// <summary>For logs and the audit: the measured strides and what was missing.</summary>
    public string OwnAnimationNote => _clips?.Note ?? "";
    private ClipSet _clips;

    /// <summary>Which of the eight creature clips a humanoid clip name means.</summary>
    public static string CreatureClipFor(string name)
    {
        string n = name.ToLowerInvariant();
        foreach (var c in CreatureClips) if (n == c.ToLowerInvariant() || n == c.ToLowerInvariant() + "_loop") return c;
        if (n.Contains("death")) return "Death";
        if (n.StartsWith("hit_")) return "Hit";
        if (n == "laytoidle" || n.Contains("spawn")) return "Spawn";
        if (n.Contains("jump") || n.Contains("roll") || n.Contains("dash") || n.Contains("slide")) return "Attack2";      // leaps and lunges
        if (n.Contains("walk") || n.Contains("crouch_fwd") || n.Contains("swim_fwd")) return "Walk";
        if (n.Contains("jog") || n.Contains("sprint")) return "Run";
        if (n.Contains("idle")) return "Idle";
        if (n.Contains("spell") || n.Contains("throw") || n.Contains("pistol") || n.Contains("interact") || n.Contains("heavy") || n == "sword_attack" || n.Contains("pickup")) return "Attack2";
        if (n.Contains("sword") || n.Contains("punch") || n.Contains("melee") || n.Contains("scratch") || n.Contains("shield")) return "Attack1";
        return "Idle";
    }

    private static ClipSet OwnClips(string fileId, Node3D inst, Skeleton3D skel)
    {
        if (fileId == null) return null;
        if (OwnSets.TryGetValue(fileId, out var cached)) return cached;
        ClipSet set = null;
        try { set = BuildOwnClips(fileId, inst, skel); }
        catch (Exception e) { Log.Error("model", $"reading the animations inside '{fileId}' failed; it uses the shared library", e); }
        OwnSets[fileId] = set;
        return set;
    }

    private static ClipSet BuildOwnClips(string fileId, Node3D inst, Skeleton3D skel)
    {
        var ap = Walk(inst).OfType<AnimationPlayer>().FirstOrDefault();
        if (ap == null || skel == null) return null;
        var found = new Dictionary<string, Animation>();
        foreach (var libName in ap.GetAnimationLibraryList())
        {
            var lib = ap.GetAnimationLibrary(libName);
            foreach (var an in lib.GetAnimationList())
            {
                string n = an.ToString();
                foreach (var c in CreatureClips)
                    if (!found.ContainsKey(c) && (n.Equals(c, StringComparison.OrdinalIgnoreCase) || n.Equals(c + "_Loop", StringComparison.OrdinalIgnoreCase)))
                        found[c] = (Animation)lib.GetAnimation(an).Duplicate();
            }
        }
        // a humanoid file that happens to keep a stray clip is not a creature rig: it needs an idle and at least one more of the eight
        if (!found.ContainsKey("Idle") && found.Count < 3) return null;
        if (found.Count < 2) return null;
        EnsureBaseLibrary();
        var set = new ClipSet { Lib = new AnimationLibrary() };
        // tracks are written against the file's own root, which is also the root our player uses; a player parented
        // deeper in the file would need its paths moved (kept simple: warn)
        var apRootNode = ap.GetNodeOrNull(ap.RootNode);
        string apRoot = apRootNode == null || apRootNode == inst ? "" : inst.GetPathTo(apRootNode).ToString();
        foreach (var kv in found)
        {
            bool loops = kv.Key is "Idle" or "Walk" or "Run";
            kv.Value.LoopMode = loops ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
            if (apRoot.Length > 0 && apRoot != ".")
                for (int t = 0; t < kv.Value.GetTrackCount(); t++) kv.Value.TrackSetPath(t, apRoot + "/" + kv.Value.TrackGetPath(t));
            set.Lib.AddAnimation(kv.Key, kv.Value);
        }
        var missing = new List<string>();
        foreach (var c in CreatureClips)
        {
            if (found.ContainsKey(c)) continue;
            missing.Add(c);
            string use = ClipFallback[c].FirstOrDefault(found.ContainsKey) ?? found.Keys.First();
            set.Lib.AddAnimation(c, found[use]);
        }
        // measured once per file: how fast the ground passes under the walk and the run, and when the attacks strike
        string skelPath = inst.GetPathTo(skel).ToString();
        var rel = Transform3D.Identity;
        for (Node n = skel; n != null && n != inst; n = n.GetParent()) if (n is Node3D n3) rel = n3.Transform * rel;
        var type = Bestiary.TypeOfModel(fileId);
        float design = type != null && type.Speed > 0.1f ? type.Speed / Mathf.Max(0.2f, type.Scale) : 4f;
        float walk = found.ContainsKey("Walk") ? GroundSpeed(found["Walk"], skel, skelPath, rel) : 0f;
        float run = found.ContainsKey("Run") ? GroundSpeed(found["Run"], skel, skelPath, rel) : 0f;
        bool walkMeasured = walk >= 0.15f, runMeasured = run >= 0.15f;
        // no feet to measure (a serpent, a floating lantern, wings): the run covers the creature's design speed at rate 1
        if (!runMeasured) run = walkMeasured ? MathF.Max(design, walk * 1.6f) : design;
        if (!walkMeasured) walk = MathF.Min(run * 0.45f, 1.6f);
        if (!found.ContainsKey("Run")) run = walk;            // the walk clip plays for both
        if (!found.ContainsKey("Walk")) walk = run;
        // the artist's numbers (rigs_bf_meta.json) win over the measurement: the generator knows its own gait and hit frames
        var meta = CreatureMeta(fileId);
        string metaNote = "";
        if (meta != null)
        {
            if (meta.Walk > 0.05f) { walk = meta.Walk; walkMeasured = true; }
            if (meta.Run > 0.05f) { run = meta.Run; runMeasured = true; }
            metaNote = " (strides and hit times from rigs_bf_meta.json)";
        }
        set.WalkStride = Mathf.Clamp(walk, 0.15f, 12f); set.RunStride = Mathf.Clamp(run, 0.3f, 20f);
        foreach (var c in new[] { "Attack1", "Attack2" })
            if (found.ContainsKey(c))
            {
                float hit = c == "Attack1" ? meta?.Hit1 ?? 0f : meta?.Hit2 ?? 0f;
                set.Strike[c] = hit > 0.01f ? hit : StrikeTime(found[c], skel, skelPath, rel);
            }
        // every name the humanoid library knows becomes an alias of one of the eight
        if (_baseLib != null)
            foreach (var an in _baseLib.GetAnimationList())
            {
                string name = an.ToString();
                string target = CreatureClipFor(name);
                if (!set.Lib.HasAnimation(name)) set.Lib.AddAnimation(name, set.Lib.GetAnimation(target));
                if (Stride.TryGetValue(name, out float humanoid)) set.RateMul[name] = humanoid / (target == "Walk" ? set.WalkStride : set.RunStride);
                if (target is "Attack1" or "Attack2" && set.Strike.TryGetValue(target, out float st)) set.Strike[name] = st;
            }
        set.Note = $"own clips [{string.Join(",", found.Keys)}]{(missing.Count > 0 ? $" missing [{string.Join(",", missing)}]" : "")} " +
            $"walk {set.WalkStride:0.00} m/s{(walkMeasured ? "" : " (assumed)")} run {set.RunStride:0.00} m/s{(runMeasured ? "" : " (assumed)")} " +
            $"strikes {string.Join(" ", new[] { "Attack1", "Attack2" }.Where(found.ContainsKey).Select(c => $"{c}={set.Strike[c]:0.00}/{found[c].Length:0.00}s"))}{metaNote}";
        Log.Info("model", $"'{fileId}' animates with its {set.Note}");
        if (missing.Count > 0) Log.Warn("model", $"'{fileId}' lacks the clips [{string.Join(",", missing)}]; the nearest of its other clips plays instead");
        return set;
    }

    private sealed class MetaEntry { public float Walk, Run, Hit1, Hit2; }
    private static Dictionary<string, MetaEntry> _meta;

    /// <summary>
    /// The creature generator's measurements for a model (assets/chars/rigs_bf_meta.json: an object keyed by model id, or a
    /// list of entries with "model"; fields walk_speed, run_speed — m/s at scale 1 and rate 1 — attack1_hit_time,
    /// attack2_hit_time). Null when the file or the entry is missing.
    /// </summary>
    private static MetaEntry CreatureMeta(string fileId)
    {
        if (_meta == null)
        {
            _meta = new Dictionary<string, MetaEntry>();
            const string path = "res://assets/chars/rigs_bf_meta.json";
            try
            {
                if (FileAccess.FileExists(path))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString(path));
                    var root = doc.RootElement;
                    IEnumerable<(string, System.Text.Json.JsonElement)> entries = Enumerable.Empty<(string, System.Text.Json.JsonElement)>();
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("models", out var models)) root = models;
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                        entries = root.EnumerateArray().Select(e => (e.TryGetProperty("model", out var id) ? id.GetString() : null, e)).ToList();
                    else if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                        entries = root.EnumerateObject().Select(p => (p.Name, p.Value)).ToList();
                    float F(System.Text.Json.JsonElement e, string k) => e.ValueKind == System.Text.Json.JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? (float)v.GetDouble() : 0f;
                    foreach (var (id, e) in entries)
                        if (!string.IsNullOrEmpty(id)) _meta[id] = new MetaEntry { Walk = F(e, "walk_speed"), Run = F(e, "run_speed"), Hit1 = F(e, "attack1_hit_time"), Hit2 = F(e, "attack2_hit_time") };
                    Log.Info("model", $"rigs_bf_meta.json: {_meta.Count} creatures");
                }
            }
            catch (Exception e) { Log.Error("model", "rigs_bf_meta.json could not be read; strides are measured from the clips", e); }
        }
        return fileId != null && _meta.TryGetValue(fileId, out var m) ? m : null;
    }

    /// <summary>Positions of every bone (model space, scale 1) at one moment of a clip, by forward kinematics on the tracks.</summary>
    private static Vector3[] PoseAt(Animation a, Skeleton3D skel, string skelPath, Transform3D rel, double time, (int pos, int rot, int scl)[] tracks, Transform3D[] scratch)
    {
        int n = skel.GetBoneCount();
        var res = new Vector3[n];
        for (int b = 0; b < n; b++)
        {
            var rest = skel.GetBoneRest(b);
            Vector3 pos = tracks[b].pos >= 0 ? a.PositionTrackInterpolate(tracks[b].pos, time) : rest.Origin;
            Quaternion rot = tracks[b].rot >= 0 ? a.RotationTrackInterpolate(tracks[b].rot, time) : rest.Basis.GetRotationQuaternion();
            Vector3 scl = tracks[b].scl >= 0 ? a.ScaleTrackInterpolate(tracks[b].scl, time) : rest.Basis.Scale;
            var local = new Transform3D(new Basis(rot).Scaled(scl), pos);
            int parent = skel.GetBoneParent(b);
            scratch[b] = parent >= 0 && parent < b ? scratch[parent] * local : local;
            res[b] = rel * scratch[b].Origin;
        }
        return res;
    }

    private static (int pos, int rot, int scl)[] BoneTracks(Animation a, Skeleton3D skel, string skelPath)
    {
        int n = skel.GetBoneCount();
        var tracks = new (int pos, int rot, int scl)[n];
        for (int b = 0; b < n; b++) tracks[b] = (-1, -1, -1);
        for (int t = 0; t < a.GetTrackCount(); t++)
        {
            string path = a.TrackGetPath(t).ToString();
            int colon = path.LastIndexOf(':');
            if (colon <= 0) continue;
            int b = skel.FindBone(path[(colon + 1)..]);
            if (b < 0) continue;
            switch (a.TrackGetType(t))
            {
                case Animation.TrackType.Position3D: tracks[b].pos = t; break;
                case Animation.TrackType.Rotation3D: tracks[b].rot = t; break;
                case Animation.TrackType.Scale3D: tracks[b].scl = t; break;
            }
        }
        return tracks;
    }

    private static List<int> TipBones(Skeleton3D skel)
    {
        int n = skel.GetBoneCount();
        var hasChild = new bool[n];
        for (int b = 0; b < n; b++) { int p = skel.GetBoneParent(b); if (p >= 0) hasChild[p] = true; }
        var tips = new List<int>();
        for (int b = 0; b < n; b++) if (!hasChild[b]) tips.Add(b);
        if (tips.Count < 2) { tips.Clear(); for (int b = 0; b < n; b++) tips.Add(b); }
        return tips;
    }

    /// <summary>
    /// Ground speed of a locomotion cycle at rate 1 (model forward is +Z): the mean backward speed of the limb tips while
    /// they are in contact with the ground. Zero when nothing touches down and travels back (floaters, wings, serpents).
    /// </summary>
    private static float GroundSpeed(Animation a, Skeleton3D skel, string skelPath, Transform3D rel)
    {
        double len = a.Length;
        if (len < 0.05 || skel.GetBoneCount() == 0) return 0f;
        const int N = 32;
        var tracks = BoneTracks(a, skel, skelPath);
        var scratch = new Transform3D[skel.GetBoneCount()];
        var tips = TipBones(skel);
        var poses = new Vector3[N + 1][];
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i <= N; i++)
        {
            poses[i] = PoseAt(a, skel, skelPath, rel, len * i / N, tracks, scratch);
            foreach (var p in poses[i]) { lo = MathF.Min(lo, p.Y); hi = MathF.Max(hi, p.Y); }
        }
        float band = MathF.Max(0.03f, (hi - lo) * 0.06f), dt = (float)(len / N);
        float sum = 0f; int count = 0;
        for (int i = 0; i < N; i++)
        {
            float ground = float.MaxValue;
            foreach (int b in tips) ground = MathF.Min(ground, poses[i][b].Y);
            float back = 0f; int nb = 0;
            foreach (int b in tips)
            {
                if (poses[i][b].Y > ground + band) continue;
                float vz = (poses[i + 1][b].Z - poses[i][b].Z) / dt;
                if (vz < -0.02f) { back += -vz; nb++; }
            }
            if (nb > 0) { sum += back / nb; count++; }
        }
        return count >= N / 4 ? sum / count : 0f;
    }

    /// <summary>When an attack clip strikes: the moment its fastest tip peaks (ignoring the first tenth, where blends pop).</summary>
    private static float StrikeTime(Animation a, Skeleton3D skel, string skelPath, Transform3D rel)
    {
        double len = a.Length;
        if (len < 0.1 || skel.GetBoneCount() == 0) return (float)len * 0.45f;
        const int N = 40;
        var tracks = BoneTracks(a, skel, skelPath);
        var scratch = new Transform3D[skel.GetBoneCount()];
        var tips = TipBones(skel);
        Vector3[] prev = PoseAt(a, skel, skelPath, rel, 0, tracks, scratch);
        float best = 0f, bestT = (float)len * 0.45f, dt = (float)(len / N);
        for (int i = 1; i <= N; i++)
        {
            var cur = PoseAt(a, skel, skelPath, rel, len * i / N, tracks, scratch);
            if (i > N / 10 && i < N * 19 / 20)
                foreach (int b in tips)
                {
                    float v = cur[b].DistanceTo(prev[b]) / dt;
                    if (v > best) { best = v; bestT = (float)(len * (i - 0.5) / N); }
                }
            prev = cur;
        }
        return best < 0.3f ? (float)len * 0.45f : bestT;
    }

    // ───────────── stealth ─────────────

    private List<(MeshInstance3D mi, int surface, Material mat)> _glow;
    private static Material _hiddenMat;
    private float _visibility = 1f;

    /// <summary>
    /// How visible the model is: 1 normal, less for a stalker that hides (0.12 in the design). While it is below 1 the
    /// model casts no shadow and its glowing parts (emis_ and spectral_ materials) are switched off — a pair of ember
    /// eyes floating through the dusk would give it away.
    /// </summary>
    public void SetVisibility(float v)
    {
        v = Mathf.Clamp(v, 0f, 1f);
        if (Mathf.Abs(v - _visibility) < 0.005f) return;
        bool wasHidden = _visibility < 0.99f, hidden = v < 0.99f;
        _visibility = v;
        foreach (var mi in _meshes)
        {
            if (!IsInstanceValid(mi)) continue;
            mi.Transparency = hidden ? 1f - v : 0f;
            if (hidden != wasHidden && !_dissolving) mi.CastShadow = hidden ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On;
        }
        if (hidden == wasHidden) return;
        if (_glow == null)
        {
            _glow = new List<(MeshInstance3D, int, Material)>();
            foreach (var mi in _meshes)
            {
                if (!IsInstanceValid(mi) || mi.Mesh == null) continue;
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    var mat = mi.GetSurfaceOverrideMaterial(s) ?? mi.GetActiveMaterial(s);
                    string name = mat?.ResourceName ?? "";
                    if (name.StartsWith("emis") || name.StartsWith("spectral")) _glow.Add((mi, s, mat));
                }
            }
        }
        _hiddenMat ??= new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0, 0, 0, 0), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            ResourceName = "hidden",
        };
        foreach (var (mi, s, mat) in _glow) if (IsInstanceValid(mi)) mi.SetSurfaceOverrideMaterial(s, hidden ? _hiddenMat : mat);
    }

    public float Visibility => _visibility;

    // ───────────── primitives ─────────────

    /// <summary>
    /// Bodies without a file: "prim_swarm" (a cloud of flecks, the stand-in for swarm creatures), "prim_turret" (a post with
    /// a lens, the stand-in for emplacements) and "prim_eggsac" (the cocoon-mother's egg sac). No skeleton, no clips —
    /// every animation call is a no-op; tint, flash and the ash dissolve work as on any model.
    /// </summary>
    private void BuildPrimitive(string kind)
    {
        IsStandIn = kind != ModelId;
        _inst = new Node3D { Name = "Prim" };
        AddChild(_inst);
        MeshInstance3D Part(Mesh mesh, Vector3 pos, Vector3 scale, Color albedo, float rough, Color? emission = null, string name = "skin_prim")
        {
            string key = $"{albedo}|{rough}|{emission}";
            if (!PrimMats.TryGetValue(key, out var mat))
            {
                mat = new StandardMaterial3D { AlbedoColor = albedo, Roughness = rough, ResourceName = emission.HasValue ? "emis_prim" : name };
                if (emission.HasValue) { mat.EmissionEnabled = true; mat.Emission = emission.Value; mat.EmissionEnergyMultiplier = 1.6f; }
                PrimMats[key] = mat;
            }
            var mi = new MeshInstance3D { Mesh = mesh, Position = pos, Scale = scale };
            mi.SetSurfaceOverrideMaterial(0, mat);
            _inst.AddChild(mi);
            return mi;
        }
        switch (kind)
        {
            case "prim_swarm":
            {
                var fleck = new PrismMesh { Size = new Vector3(0.09f, 0.06f, 0.02f) };
                var rng = new Random(ModelId?.GetHashCode() ?? 7);
                for (int i = 0; i < 26; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.Tau, r = 0.25f + 0.75f * MathF.Sqrt((float)rng.NextDouble()), h = 0.5f + (float)rng.NextDouble() * 1.4f;
                    var mi = Part(fleck, new Vector3(MathF.Cos(a) * r, h, MathF.Sin(a) * r), Vector3.One * (0.8f + (float)rng.NextDouble()), new Color(0.32f, 0.3f, 0.27f), 0.9f);
                    mi.Rotation = new Vector3((float)rng.NextDouble() * 3f, a, (float)rng.NextDouble() * 3f);
                }
                Height = 2f;
                break;
            }
            case "prim_eggsac":
                Part(new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 8 }, new Vector3(0, 0.42f, 0), new Vector3(1f, 0.85f, 1f), new Color(0.78f, 0.76f, 0.66f), 0.55f);
                Part(new SphereMesh { Radius = 0.22f, Height = 0.44f, RadialSegments = 10, Rings = 6 }, new Vector3(0.05f, 0.5f, 0.1f), Vector3.One, new Color(0.5f, 0.7f, 0.3f), 0.4f, new Color(0.45f, 0.8f, 0.25f));
                Height = 0.9f;
                break;
            default:    // prim_turret
                Part(new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.3f, Height = 1.3f, RadialSegments = 10 }, new Vector3(0, 0.65f, 0), Vector3.One, new Color(0.36f, 0.31f, 0.24f), 0.7f);
                Part(new SphereMesh { Radius = 0.34f, Height = 0.68f, RadialSegments = 14, Rings = 8 }, new Vector3(0, 1.5f, 0), new Vector3(1f, 1f, 0.6f), new Color(0.5f, 0.42f, 0.28f), 0.4f);
                Part(new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 10, Rings = 6 }, new Vector3(0, 1.5f, 0.2f), Vector3.One, new Color(0.9f, 0.7f, 0.3f), 0.3f, new Color(1f, 0.75f, 0.3f));
                Height = 1.9f;
                break;
        }
        foreach (var mi in Walk(_inst).OfType<MeshInstance3D>())
        {
            _meshes.Add(mi);
            ConvertMaterials(mi);
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        }
    }

    private static readonly Dictionary<string, StandardMaterial3D> PrimMats = new();

    /// <summary>True for a body built from primitives (no skeleton by design: not an animation fault).</summary>
    public bool IsPrimitive => FileId != null && FileId.StartsWith("prim_", StringComparison.Ordinal);

    private bool _asleep;

    /// <summary>
    /// Stops (or resumes) the animation work of a character nobody can see. A region holds a few hundred monsters and
    /// every playing AnimationPlayer costs CPU each frame whether it is on screen or not.
    /// </summary>
    public void SetAsleep(bool asleep)
    {
        if (_asleep == asleep) return;
        _asleep = asleep;
        if (Anim != null && IsInstanceValid(Anim)) Anim.Active = !asleep && !NoAnimation;
    }

    // only the hit flash and the ash dissolve need per-frame work; the model sleeps otherwise
    public override void _Ready() => SetProcess(_flash > 0f || _dissolving || _restoreIn > 0f);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_flash > 0f)
        {
            _flash = Mathf.Max(0f, _flash - dt * 13f);        // about 75 ms from full
            SetInst(PFlash, _flash);
        }
        if (_dissolving && _dissolve < 1f)
        {
            _dissolve = Mathf.Min(1f, _dissolve + dt * DissolveSpeed);
            SetInst(PDissolve, _dissolve);
        }
        if (_restoreIn > 0f)
        {
            _restoreIn -= dt;
            if (_restoreIn <= 0f && Anim != null && IsInstanceValid(Anim)) Anim.SpeedScale = _restoreRate;
        }
        if (_flash <= 0f && !(_dissolving && _dissolve < 1f) && _restoreIn <= 0f) SetProcess(false);
    }
}
