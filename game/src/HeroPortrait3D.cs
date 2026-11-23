using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// A live portrait of a hero for character sheets: the real <see cref="CharModel"/> (same file, same character shader,
/// same shared animation library, the weapon meshes the hero carries in the world) idling on a small stage of its own.
///
/// The stage lives in a <see cref="SubViewport"/> with its own <see cref="World3D"/>, so nothing of the level shows
/// in it and the level never sees it: a warm key light from the upper front-left (the only shadow caster), a cool
/// rim from behind, a faint ember from below, an environment of its own (flat ambient, the game's ACES curve, no
/// fog), a lit floor disc with a contact shadow and a dark backdrop that falls off to the black of a panel well.
/// The camera is framed on the measured bounds of the figure over its whole idle loop, weapon included, so every
/// class and every model scale fits with the same head-room.
///
/// Sharp at every size: the viewport is rendered at the control's size in real screen pixels (design size × the
/// window's stretch), whatever the text-size setting or the resolution. Cheap: it renders only while the control is
/// visible, and <see cref="Unload"/> drops the model when the owner closes.
///
/// The player may drag with the left button to turn the figure; it eases back to the front a moment after release.
/// Use: add it to a panel, give it a rectangle, call <see cref="SetHero"/> (it follows <see cref="Hero.Changed"/> by
/// itself) and <see cref="Unload"/> when the panel closes.
/// </summary>
public partial class HeroPortrait3D : Control
{
    // ── framing ──
    private const float FovDeg = 24f;               // long lens: no wide-angle bulge on a figure that fills the frame
    private const float PitchDeg = 7f;              // the lens looks down a little, so the floor reads as a floor
    private const float HeadRoom = 0.055f, FootRoom = 0.095f, SideRoom = 0.05f;   // shares of the picture kept free
    // ── motion ──
    private const float BaseYaw = 0.30f;            // three-quarter view: the weapon arm toward the lens
    private const float SwayAmp = 0.075f, SwayRate = 0.42f;
    private const float DragRadPerPx = 0.0125f, ReturnDelay = 2.4f, ReturnRate = 3.4f;
    // ── look ──
    private static readonly Vector3 GradeTint = new(0.74f, 0.74f, 0.74f);      // the level grades characters per theme; the portrait is the same in every region
    private const float GradeDesat = 0.10f;
    private const int MaxPx = 2048;
    // ── anti-aliasing (no MSAA, see BuildStage) ──
    private const float SuperMax = 2f;                      // at most 2×2 rendered pixels per screen pixel
    private const float BudgetPx = 900_000f, BudgetPxLow = 450_000f;   // rendered pixels at most (低: half)
    private const float FxaaBelow = 1.5f;                   // too few samples per pixel for clean edges: FXAA on top
    private const uint FigureLayer = 1, FloorLayer = 2;     // only the key light reaches the floor; rim and ember belong to the figure

    /// <summary>False: the figure cannot be turned with the mouse and the control lets every click through.</summary>
    public bool AllowDrag { get; set; } = true;
    /// <summary>The model on the stage (null while nothing is shown).</summary>
    public CharModel Model => _model;
    /// <summary>How far the player has turned the figure from its resting view, in radians.</summary>
    public float Yaw => _yaw;

    private SubViewportContainer _box;
    private SubViewport _vp;
    private Node3D _stage, _pivot;
    private Camera3D _cam;
    private SpotLight3D _key;
    private DirectionalLight3D _rim, _kick;
    private OmniLight3D _ember;
    private MeshInstance3D _floor, _blob, _backdrop;
    private CpuParticles3D _motes;
    private ShaderMaterial _floorMat, _blobMat, _backMat;
    private Godot.Environment _env;

    private CharModel _model;
    private string _modelId, _idle = "", _wpnSig = "";
    private string _ownedSig = "";                 // the gear signature whose materials the portrait owns copies of
    private readonly Dictionary<ulong, ShaderMaterial> _mats = new();       // the level's shared material → the portrait's own copy
    private Hero _hero;
    private int _class = -1;                        // shown without a Hero (SetClass)
    private int _classTier = 1;
    private bool _syncPending, _measureDirty, _fitDirty, _snap = true, _active;
    private Vector2I _px;
    private Vector2 _fitSize;
    private readonly List<Vector3> _cloud = new();  // hull points of every sampled idle pose, in the figure's own axes: what the lens must hold
    private Vector2 _feet;
    private float _height = 1.8f, _floorY;
    private float _camDist, _goalDist, _restDist, _roundDist;                // where the lens is, where it is going; the fit at rest and for any turn
    private Vector3 _camTarget, _goalTarget, _restTarget, _roundTarget;
    private float _yaw, _hold, _t;
    private bool _dragging;
    private int _quality = -1;
    private float _ss = 1f;                         // rendered pixels per screen pixel, per axis
    private float _bright = -1f;

    public HeroPortrait3D()
    {
        Name = "HeroPortrait";
        MouseFilter = MouseFilterEnum.Pass;
        ProcessMode = ProcessModeEnum.Always;       // the idle goes on while the game stands still behind a panel
        ClipContents = true;
        Log.Guard("portrait", BuildStage);
    }

    // ───────────────────────── what to show ─────────────────────────

    /// <summary>
    /// Shows this hero as he or she stands in the world (model of the class or of the form taken, visible weapon,
    /// class idle, scale) and keeps following <see cref="Hero.Changed"/>. Null clears the stage.
    /// </summary>
    public void SetHero(Hero hero)
    {
        _class = -1;
        if (!ReferenceEquals(hero, _hero))
        {
            if (_hero != null) _hero.Changed -= OnHeroChanged;
            _hero = hero;
            if (_hero != null) _hero.Changed += OnHeroChanged;
        }
        if (hero == null) { DropModel(); return; }
        Sync();
    }

    /// <summary>Shows a class without a hero behind it (a class-choice screen): its model, idle and its signature look (weaponTier is kept for old callers).</summary>
    public void SetClass(ClassId c, int weaponTier = 1)
    {
        if (_hero != null) { _hero.Changed -= OnHeroChanged; _hero = null; }
        _class = (int)c;
        _classTier = Math.Clamp(weaponTier, 1, 3);
        Sync();
    }

    /// <summary>Brings the figure in step with the hero on the next frame (model, weapon, scale). Cheap when nothing changed.</summary>
    public void Sync()
    {
        _syncPending = true;        // done by the next frame of the control: by then it has its place and its pixels
    }

    /// <summary>
    /// The owner closed: the model is freed (its textures belong to the level again), nothing is rendered and the
    /// render target shrinks to a stub. The next <see cref="SetHero"/> builds everything afresh in a few milliseconds.
    /// </summary>
    public void Unload()
    {
        if (_hero != null) { _hero.Changed -= OnHeroChanged; _hero = null; }
        _class = -1;
        _syncPending = false;
        _dragging = false;
        _yaw = 0f; _hold = 0f;
        DropModel();
        if (_box != null)
        {
            _px = default; _fitSize = default;      // the next frame on screen sizes the viewport again
            _box.Scale = Vector2.One;
            _box.Size = new Vector2(16f, 16f);
        }
    }

    private void OnHeroChanged()
    {
        if (IsInstanceValid(this)) _syncPending = true;
    }

    private void SyncNow()
    {
        _syncPending = false;
        if (!_offAsked) { _offAsked = true; _off = Game.I != null && Game.I.Arg("portrait-off"); }
        if (_off || (_hero == null && _class < 0)) { DropModel(); return; }
        ClassId cls = _hero != null ? _hero.Class : (ClassId)_class;

        // the hero's body in the world, when this is the hero being played: its model, form, scale and weapon are mirrored
        CharModel world = null;
        int form = 0;
        var pl = Game.I?.Player;
        if (_hero != null && pl != null && IsInstanceValid(pl) && ReferenceEquals(pl.Hero, _hero))
        {
            world = pl.ActiveModel;
            form = pl.Form;
            if (world == null || !IsInstanceValid(world) || world.ModelId == null) { world = null; form = 0; }
        }
        string id = world?.ModelId ?? "hero_" + Names.ClassKey(cls);
        float scale = world != null ? Mathf.Clamp(world.Scale.Y, 0.2f, 4f) : 1f;
        string idle = Player.IdleAnimOf(cls, form);          // the idle the hero stands in in the world
        // test switch --portrait-model=<file id>[@scale]: any character file on the stage (a shape-shifted form, a scaled body)
        if (!_forceAsked)
        {
            _forceAsked = true;
            string f = Game.I?.ArgValue("portrait-model");
            if (!string.IsNullOrWhiteSpace(f))
            {
                string[] p = f.Split('@');
                _forceId = p[0].Trim();
                if (p.Length > 1 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fs)) _forceScale = Mathf.Clamp(fs, 0.2f, 4f);
            }
        }
        if (_forceId != null)
        {
            if (!_forceId.StartsWith("hero_", StringComparison.Ordinal)) { world = null; idle = Player.IdleAnimOf(cls, 1); }
            id = _forceId; scale = _forceScale;
        }

        if (_model == null || id != _modelId) BuildModel(id, cls);
        if (_model == null) return;
        // the exact equipment (armour pieces, weapons, stance) of the hero, or of the live model when it is the one played
        if (_model.GearManaged)
        {
            var gear = world != null && world.Gear != null && world.ModelId == _model.ModelId ? world.Gear
                : _hero != null ? GearSet.FromHero(_hero) : GearSet.Showcase(cls);     // a class shown without a hero: its signature look
            _model.SetGear(gear);
            if (_model.GearSignature != _ownedSig) { OwnMaterials(_model); _ownedSig = _model.GearSignature; }
            if (form == 0 && _forceId == null) idle = _model.StanceIdle;
        }
        if (!Mathf.IsEqualApprox(_model.Scale.Y, scale)) { _model.Scale = Vector3.One * scale; _measureDirty = true; }
        if (_idle != idle)
        {
            _idle = idle;
            _model.Play(idle, 1f, 0f, true);
            _measureDirty = true;
        }
        string sig = SyncWeapons(world);
        if (sig != _wpnSig) { _wpnSig = sig; _measureDirty = true; }
        Activate(IsVisibleInTree());
        if (_measureDirty && IsInsideTree()) Refit();
    }

    /// <summary>Shows the weapon meshes the hero shows in the world; returns their names (a change means new bounds).</summary>
    private string SyncWeapons(CharModel world)
    {
        if (_model.Gear != null) return _model.GearSignature;
        var mine = new List<MeshInstance3D>();
        foreach (var mi in Meshes(_model)) if (IsWeapon(mi)) mine.Add(mi);
        if (mine.Count == 0) return "";
        Dictionary<string, bool> live = null;
        if (world != null && world.FileId == _model.FileId)
        {
            live = new Dictionary<string, bool>();
            foreach (var mi in Meshes(world)) if (IsWeapon(mi)) live[mi.Name.ToString()] = mi.Visible;
            if (live.Count != mine.Count) live = null;
        }
        if (live != null) foreach (var mi in mine) mi.Visible = live.TryGetValue(mi.Name.ToString(), out bool on) && on;
        else _model.SetWeaponTier(_hero != null ? Player.WeaponTierOf(_hero.Weapon) : _classTier);     // the world's rule when no live model can be mirrored
        var sb = new System.Text.StringBuilder();
        foreach (var mi in mine) if (mi.Visible) sb.Append(mi.Name.ToString()).Append(' ');
        return sb.ToString();
    }

    private static bool IsWeapon(MeshInstance3D mi) => mi.Name.ToString().StartsWith("wpn_", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<MeshInstance3D> Meshes(Node n)
    {
        if (n is MeshInstance3D mi) yield return mi;
        foreach (var c in n.GetChildren()) foreach (var d in Meshes(c)) yield return d;
    }

    // ───────────────────────── model ─────────────────────────

    private void BuildModel(string id, ClassId cls)
    {
        ulong t0 = Time.GetTicksUsec();
        DropModel();
        CharModel m = null;
        try { m = CharModel.Create(id); }
        catch (Exception e) { Log.Error("portrait", $"model '{id}' could not be created", e); }
        if (m == null || m.GetChildCount() == 0)
        {
            m?.Free();
            Log.Warn("portrait", $"no model for '{id}': the portrait stays empty");
            return;
        }
        _model = m;
        _modelId = id;
        _pivot.AddChild(m);
        OwnMaterials(m);
        _ownedSig = m.GearSignature;
        m.GearChanged += () => { if (IsInstanceValid(this)) _syncPending = true; };     // a loadout that waited for its files arrived
        // the same hint of the class colour on the edges that the hero carries in the world
        m.SetRim(Names.ClassColor(cls) * 0.35f);
        _idle = ""; _wpnSig = "";
        _measureDirty = true; _snap = true;
        _yaw = 0f; _hold = 0f; _dragging = false;
        Color cc = Names.ClassColor(cls);
        if (_backMat != null)
        {
            // a breath of the class colour in the haze behind the figure
            var haze = new Color(0.046f, 0.048f, 0.058f) + new Color(cc.R, cc.G, cc.B) * 0.010f;
            _backMat.SetShaderParameter("inner", new Vector3(haze.R, haze.G, haze.B));
        }
        Log.Info("portrait", $"model {id} (file {m.FileId}{(m.IsStandIn ? ", stand-in" : "")}) built in {(Time.GetTicksUsec() - t0) / 1000.0:0.0} ms");
    }

    private void DropModel()
    {
        if (_model != null)
        {
            if (IsInstanceValid(_model))
            {
                if (_model.GetParent() != null) _model.GetParent().RemoveChild(_model);
                _model.QueueFree();
            }
            _model = null;
        }
        _modelId = null; _idle = ""; _wpnSig = "";
        _mats.Clear();
        Activate(false);
    }

    /// <summary>
    /// The character shader takes its grading (tint into the value band of the region, desaturation, the cool edge
    /// away from the level camera) from uniforms that CharModel.SetSceneGrade rewrites for every level on the SHARED
    /// materials. The portrait wears copies with a fixed, neutral grade — textures and the shader stay shared — so it
    /// looks the same in every region and the level is not touched.
    /// </summary>
    private void OwnMaterials(CharModel m)
    {
        foreach (var mi in Meshes(m))
        {
            if (mi.Mesh == null) continue;
            int n = mi.Mesh.GetSurfaceCount();
            for (int s = 0; s < n; s++)
            {
                if (mi.GetSurfaceOverrideMaterial(s) is not ShaderMaterial src || src.Shader == null) continue;
                if (!src.Shader.ResourcePath.EndsWith("char.gdshader", StringComparison.Ordinal)) continue;
                if (_mats.ContainsValue(src)) continue;          // already the portrait's own copy (gear changed: only new pieces need one)
                if (!_mats.TryGetValue(src.GetInstanceId(), out var own))
                {
                    own = (ShaderMaterial)src.Duplicate();
                    own.SetShaderParameter("grade_tint", GradeTint);
                    own.SetShaderParameter("grade_desat", GradeDesat);
                    own.SetShaderParameter("scene_rim", Vector3.Zero);
                    _mats[src.GetInstanceId()] = own;
                }
                mi.SetSurfaceOverrideMaterial(s, own);
            }
        }
    }

    // ───────────────────────── bounds ─────────────────────────

    /// <summary>
    /// What is left of a skinned mesh for measuring: for every bone, the outermost vertices (14 directions) of the part
    /// that follows it, in that bone's space. Posed by the skeleton they bound the animated mesh closely, weapon and
    /// crest included. Read back from the mesh once per file and mesh for the whole session.
    /// </summary>
    private sealed class Proxy
    {
        public int[] Bone;          // skeleton bone index per point, -1 = the mesh is not skinned (points in the node's own space)
        public Vector3[] Pt;
    }

    private static readonly Dictionary<string, Proxy> Proxies = new();
    private static readonly Vector3[] Dirs = BuildDirs();

    private static Vector3[] BuildDirs()
    {
        var d = new List<Vector3> { Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward };
        for (int i = 0; i < 8; i++) d.Add(new Vector3((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1).Normalized());
        return d.ToArray();
    }

    private static Proxy ProxyOf(string key, MeshInstance3D mi, Skeleton3D sk)
    {
        if (Proxies.TryGetValue(key, out var p)) return p;
        try { p = BuildProxy(mi, sk); }
        catch (Exception e) { Log.Error("portrait", $"measuring mesh '{key}' failed; its box at rest is used", e); p = null; }
        p ??= BoxProxy(mi);
        Proxies[key] = p;
        return p;
    }

    private static Proxy BoxProxy(MeshInstance3D mi)
    {
        var a = mi.GetAabb();
        var pts = new Vector3[8];
        for (int i = 0; i < 8; i++) pts[i] = a.GetEndpoint(i);
        var bone = new int[8];
        Array.Fill(bone, -1);
        return new Proxy { Bone = bone, Pt = pts };
    }

    private static Proxy BuildProxy(MeshInstance3D mi, Skeleton3D sk)
    {
        var mesh = mi.Mesh;
        if (mesh == null || sk == null) return null;
        int nb = sk.GetBoneCount();
        if (nb == 0) return null;
        var skin = mi.Skin;
        int binds = skin != null ? skin.GetBindCount() : nb;
        var bone = new int[binds];
        var inv = new Transform3D[binds];
        for (int i = 0; i < binds; i++)
        {
            if (skin != null)
            {
                int b = skin.GetBindBone(i);
                if (b < 0) b = sk.FindBone(skin.GetBindName(i).ToString());
                bone[i] = b;
                inv[i] = skin.GetBindPose(i);
            }
            else { bone[i] = i; inv[i] = sk.GetBoneGlobalRest(i).AffineInverse(); }
        }
        int nd = Dirs.Length;
        var best = new float[nb * nd];
        Array.Fill(best, float.NegativeInfinity);
        var pts = new Vector3[nb * nd];
        bool any = false;
        int surfaces = mesh.GetSurfaceCount();
        for (int s = 0; s < surfaces; s++)
        {
            var arr = mesh.SurfaceGetArrays(s);
            if (arr == null || arr.Count <= (int)Mesh.ArrayType.Weights) continue;
            Vector3[] v = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Variant bv = arr[(int)Mesh.ArrayType.Bones], wv = arr[(int)Mesh.ArrayType.Weights];
            if (v == null || v.Length == 0 || bv.VariantType == Variant.Type.Nil || wv.VariantType == Variant.Type.Nil) return null;
            int[] bi = bv.VariantType == Variant.Type.PackedInt32Array ? bv.AsInt32Array() : Array.ConvertAll(bv.AsFloat32Array(), f => (int)f);
            float[] w = wv.VariantType == Variant.Type.PackedFloat64Array ? Array.ConvertAll(wv.AsFloat64Array(), d => (float)d) : wv.AsFloat32Array();
            if (bi.Length < v.Length || w.Length < bi.Length) return null;
            int per = bi.Length / v.Length;
            for (int i = 0; i < v.Length; i++)
            {
                int o = i * per, top = 0;
                for (int j = 1; j < per; j++) if (w[o + j] > w[o + top]) top = j;
                int k = bi[o + top];
                if (k < 0 || k >= binds) continue;
                int b = bone[k];
                if (b < 0 || b >= nb) continue;
                Vector3 p = inv[k] * v[i];
                int row = b * nd;
                for (int d = 0; d < nd; d++)
                {
                    float dot = p.Dot(Dirs[d]);
                    if (dot > best[row + d]) { best[row + d] = dot; pts[row + d] = p; }
                }
                any = true;
            }
        }
        if (!any) return null;
        var outBone = new List<int>();
        var outPt = new List<Vector3>();
        for (int b = 0; b < nb; b++)
        {
            if (float.IsNegativeInfinity(best[b * nd])) continue;
            for (int d = 0; d < nd; d++) { outBone.Add(b); outPt.Add(pts[b * nd + d]); }
        }
        return new Proxy { Bone = outBone.ToArray(), Pt = outPt.ToArray() };
    }

    /// <summary>
    /// Samples the idle loop and keeps, for every sampled pose, the outermost points of the figure (hull points along
    /// <see cref="HullDirs"/>) in the figure's own axes: what the lens has to hold, whichever way the figure is turned.
    /// </summary>
    private void Measure()
    {
        var m = _model;
        ulong t0 = Time.GetTicksUsec();
        var sk = m.Skeleton;
        var parts = new List<(MeshInstance3D mi, Proxy p)>();
        int fresh = 0;
        foreach (var mi in Meshes(m))
        {
            if (!mi.Visible || mi.Mesh == null) continue;
            string key = m.FileId + "/" + mi.Name;
            if (!Proxies.ContainsKey(key)) fresh++;
            parts.Add((mi, ProxyOf(key, mi, sk)));
        }
        Transform3D skel = sk != null ? RelativeTo(sk, _pivot) : Transform3D.Identity;
        int nb = sk?.GetBoneCount() ?? 0, nd = HullDirs.Length;
        var pose = new Transform3D[nb];
        var have = new bool[nb];
        var best = new float[nd];
        var bestPt = new Vector3[nd];

        void Take()
        {
            Array.Clear(have, 0, nb);
            Array.Fill(best, float.NegativeInfinity);
            foreach (var (mi, p) in parts)
            {
                Transform3D own = default;
                bool ownKnown = false;
                for (int i = 0; i < p.Pt.Length; i++)
                {
                    int b = p.Bone[i];
                    Vector3 q;
                    if (b >= 0 && b < nb)
                    {
                        if (!have[b]) { pose[b] = skel * sk.GetBoneGlobalPose(b); have[b] = true; }
                        q = pose[b] * p.Pt[i];
                    }
                    else
                    {
                        if (!ownKnown) { own = RelativeTo(mi, _pivot); ownKnown = true; }
                        q = own * p.Pt[i];
                    }
                    for (int d = 0; d < nd; d++)
                    {
                        float dot = q.X * HullDirs[d].X + q.Y * HullDirs[d].Y + q.Z * HullDirs[d].Z;
                        if (dot > best[d]) { best[d] = dot; bestPt[d] = q; }
                    }
                }
            }
            for (int d = 0; d < nd; d++) if (!float.IsNegativeInfinity(best[d])) _cloud.Add(bestPt[d]);
        }

        _cloud.Clear();
        var anim = m.Anim;
        int samples = 0;
        if (parts.Count > 0)
        {
            if (sk != null && anim != null && IsInstanceValid(anim) && anim.Active && m.Has(_idle) && anim.CurrentAnimation != "")
            {
                double len = Math.Max(0.05, anim.CurrentAnimationLength), keep = anim.CurrentAnimationPosition;
                const int N = 8;
                for (int i = 0; i < N; i++) { anim.Seek(len * i / N, true); Take(); samples++; }
                anim.Seek(Math.Clamp(keep, 0.0, len), true);
            }
            else { Take(); samples = 1; }
        }
        if (_cloud.Count == 0)
        {
            // nothing measurable: a box of the model's nominal height
            float h = Mathf.Max(0.5f, m.Height * m.Scale.Y);
            for (int i = 0; i < 8; i++) _cloud.Add(new Vector3((i & 1) != 0 ? h * 0.3f : -h * 0.3f, (i & 2) != 0 ? h : 0f, (i & 4) != 0 ? h * 0.2f : -h * 0.2f));
        }
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var p in _cloud) { lo = MathF.Min(lo, p.Y); hi = MathF.Max(hi, p.Y); }
        if (lo > -0.15f * m.Scale.Y && lo < 0.25f * m.Scale.Y) lo = 0f;     // the figure stands on the floor
        _floorY = lo; _height = Mathf.Max(0.3f, hi - lo);
        _feet = Vector2.Zero;                   // in the figure's own axes (the contact shadow turns with it)
        if (sk != null)
        {
            int fl = sk.FindBone("foot_l"), fr = sk.FindBone("foot_r");
            if (fl >= 0 && fr >= 0)
            {
                Vector3 a = skel * sk.GetBoneGlobalPose(fl).Origin, b = skel * sk.GetBoneGlobalPose(fr).Origin;
                _feet = new Vector2((a.X + b.X) * 0.5f, (a.Z + b.Z) * 0.5f);
            }
        }
        _measureDirty = false;
        Log.Info("portrait", $"figure {_modelId} [{_wpnSig.Trim()}] in '{_idle}': {_height:0.00} m tall, {_cloud.Count} hull points from {parts.Count} meshes over {samples} poses " +
            $"({fresh} meshes read back) in {(Time.GetTicksUsec() - t0) / 1000.0:0.0} ms");
    }

    private static readonly Vector3[] HullDirs = BuildHullDirs(42);

    /// <summary>Directions spread evenly over the sphere (a Fibonacci lattice), with straight up and down added.</summary>
    private static Vector3[] BuildHullDirs(int n)
    {
        var d = new List<Vector3> { Vector3.Up, Vector3.Down };
        float golden = Mathf.Pi * (3f - Mathf.Sqrt(5f));
        for (int i = 0; i < n; i++)
        {
            float y = 1f - 2f * (i + 0.5f) / n, r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), a = golden * i;
            d.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
        }
        return d.ToArray();
    }

    private static Transform3D RelativeTo(Node3D n, Node3D ancestor)
    {
        var t = Transform3D.Identity;
        for (Node c = n; c != null && c != ancestor; c = c.GetParent()) if (c is Node3D n3) t = n3.Transform * t;
        return t;
    }

    // ───────────────────────── stage ─────────────────────────

    private static Shader _floorShader, _blobShader, _backShader;     // compiled once, shared by every portrait

    private const string FloorShader = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, diffuse_burley, specular_schlick_ggx;
uniform sampler2D noise_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec3 base : source_color = vec3(0.085, 0.079, 0.074);
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float n = texture(noise_tex, UV * 1.7).r;
	float n2 = texture(noise_tex, UV * 5.3 + vec2(0.37, 0.11)).r;
	ALBEDO = base * (0.55 + 0.9 * n) * (0.75 + 0.5 * n2);
	ROUGHNESS = 0.72 + 0.25 * n2;
	SPECULAR = 0.35;
	ALPHA = 1.0 - smoothstep(0.18, 0.90, r);
}";

    private const string BlobShader = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled;
uniform float strength = 0.8;
void fragment() {
	float r = length(UV * 2.0 - 1.0);
	float k = 1.0 - smoothstep(0.0, 1.0, r);
	ALBEDO = vec3(0.0);
	ALPHA = strength * k * k;
}";

    private const string BackShader = @"
shader_type spatial;
render_mode unshaded, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec3 inner : source_color = vec3(0.046, 0.048, 0.058);
uniform vec3 outer : source_color = vec3(0.005, 0.005, 0.006);
uniform vec2 centre = vec2(0.5, 0.40);
uniform float aspect = 1.0;
void fragment() {
	vec2 p = (UV - centre) * vec2(aspect, 1.0);
	float r = length(p) * 2.1;
	float k = smoothstep(0.0, 1.0, r);
	ALBEDO = mix(inner, outer, k * (2.0 - k));
}";

    private void BuildStage()
    {
        _box = new SubViewportContainer { Name = "View", Stretch = true, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Linear };
        AddChild(_box);
        _vp = new SubViewport
        {
            Name = "Stage", World3D = new World3D(), TransparentBg = false, HandleInputLocally = false, GuiDisableInput = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Size = new Vector2I(64, 64),
            // no MSAA: the main view never uses it, and a sample count of its own would make the renderer compile new
            // pipelines for the hero's materials on the first open (a 0.9–1.4 s stall with a cold driver cache).
            // Edges are smoothed by supersampling instead (FitPixels); the shadow atlas depth follows the main view (ApplyQuality).
            Msaa3D = Viewport.Msaa.Disabled, ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled, UseTaa = false, UseDebanding = true,
            Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear, Scaling3DScale = 1f,
            PositionalShadowAtlasSize = 1024, PositionalShadowAtlas16Bits = true, AudioListenerEnable3D = false,
        };
        // one light casts a shadow: it gets the whole atlas
        _vp.SetPositionalShadowAtlasQuadrantSubdiv(0, Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv1);
        _vp.SetPositionalShadowAtlasQuadrantSubdiv(1, Viewport.PositionalShadowAtlasQuadrantSubdiv.Disabled);
        _vp.SetPositionalShadowAtlasQuadrantSubdiv(2, Viewport.PositionalShadowAtlasQuadrantSubdiv.Disabled);
        _vp.SetPositionalShadowAtlasQuadrantSubdiv(3, Viewport.PositionalShadowAtlasQuadrantSubdiv.Disabled);
        _box.AddChild(_vp);
        _stage = new Node3D { Name = "Scene" };
        _vp.AddChild(_stage);

        // what polished plate mirrors: a dim studio dome, cool above and ember-warm below (never on screen)
        var dome = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.16f, 0.20f, 0.30f), SkyHorizonColor = new Color(0.22f, 0.21f, 0.22f), SkyCurve = 0.25f,
            GroundBottomColor = new Color(0.10f, 0.05f, 0.025f), GroundHorizonColor = new Color(0.20f, 0.15f, 0.12f), GroundCurve = 0.2f,
            SunAngleMax = 0f, SunCurve = 0.01f,
        };
        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.005f, 0.005f, 0.006f),
            Sky = new Sky { SkyMaterial = dome, RadianceSize = Sky.RadianceSizeEnum.Size32, ProcessMode = Sky.ProcessModeEnum.Quality },
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.42f, 0.46f, 0.58f), AmbientLightEnergy = 0.40f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Aces, TonemapExposure = 1f, TonemapWhite = 7f,      // the curve of the levels
            GlowEnabled = true, GlowIntensity = 0.7f, GlowStrength = 1f, GlowBloom = 0f, GlowHdrThreshold = 1.05f, GlowHdrScale = 2f,
            GlowHdrLuminanceCap = 12f, GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen,
            FogEnabled = false, VolumetricFogEnabled = false, SsaoEnabled = false, SsilEnabled = false, SsrEnabled = false, SdfgiEnabled = false,
            AdjustmentEnabled = false,
        };
        _vp.World3D.Environment = _env;

        _pivot = new Node3D { Name = "Pivot" };
        _stage.AddChild(_pivot);

        _key = new SpotLight3D
        {
            Name = "Key", LightColor = new Color(1f, 0.83f, 0.64f), LightEnergy = 3.0f, LightSpecular = 0.7f,
            SpotRange = 30f, SpotAttenuation = 0f, SpotAngle = 27f, SpotAngleAttenuation = 0.55f,
            ShadowEnabled = true, ShadowBias = 0.04f, ShadowNormalBias = 1.4f, ShadowBlur = 1.8f, LightCullMask = FigureLayer | FloorLayer,
        };
        _stage.AddChild(_key);
        _rim = new DirectionalLight3D
        {
            Name = "Rim", LightColor = new Color(0.52f, 0.68f, 1f), LightEnergy = 3.8f, LightSpecular = 1f, ShadowEnabled = false,
            LightCullMask = FigureLayer, SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly,
        };
        _stage.AddChild(_rim);
        _kick = new DirectionalLight3D
        {
            Name = "Kick", LightColor = new Color(0.45f, 0.58f, 0.80f), LightEnergy = 1.5f, LightSpecular = 0.6f, ShadowEnabled = false,
            LightCullMask = FigureLayer, SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly,
        };
        _stage.AddChild(_kick);
        _ember = new OmniLight3D
        {
            Name = "Ember", LightColor = new Color(1f, 0.44f, 0.16f), LightEnergy = 0.8f, LightSpecular = 0.25f, OmniAttenuation = 1.3f,
            ShadowEnabled = false, LightCullMask = FigureLayer,
        };
        _stage.AddChild(_ember);

        _floorMat = new ShaderMaterial { Shader = _floorShader ??= new Shader { Code = FloorShader } };
        _floorMat.SetShaderParameter("noise_tex", CharModel.Noise);
        _floor = new MeshInstance3D
        {
            Name = "Floor", Mesh = new PlaneMesh { Size = Vector2.One }, MaterialOverride = _floorMat, Layers = FloorLayer,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _stage.AddChild(_floor);
        _blobMat = new ShaderMaterial { Shader = _blobShader ??= new Shader { Code = BlobShader }, RenderPriority = 1 };
        _blob = new MeshInstance3D
        {
            Name = "ContactShadow", Mesh = new PlaneMesh { Size = Vector2.One }, MaterialOverride = _blobMat, Layers = FloorLayer,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _pivot.AddChild(_blob);

        // a few embers drifting up around the figure: the air of the game, and something that moves even in a still idle
        var fade = new Gradient { Offsets = new[] { 0f, 0.18f, 0.7f, 1f }, Colors = new[] { new Color(1f, 1f, 1f, 0f), Colors.White, new Color(1f, 0.8f, 0.6f, 0.8f), new Color(1f, 0.5f, 0.3f, 0f) } };
        _motes = new CpuParticles3D
        {
            Name = "Embers", Amount = 10, Lifetime = 7.0, Preprocess = 7.0, Randomness = 1f, LocalCoords = true,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.85f, 0.3f, 0.5f),
            Direction = Vector3.Up, Spread = 22f, Gravity = new Vector3(0.012f, 0.03f, 0f), InitialVelocityMin = 0.07f, InitialVelocityMax = 0.19f,
            ScaleAmountMin = 0.55f, ScaleAmountMax = 1.25f, ColorRamp = fade, Layers = FloorLayer,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new QuadMesh
            {
                Size = new Vector2(0.017f, 0.017f),
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, BillboardKeepScale = true,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, VertexColorUseAsAlbedo = true,
                    AlbedoColor = new Color(1.6f, 0.72f, 0.22f),
                },
            },
        };
        _stage.AddChild(_motes);

        _cam = new Camera3D { Name = "Camera", Fov = FovDeg, Near = 0.2f, Far = 80f, Current = true };
        _stage.AddChild(_cam);
        _backMat = new ShaderMaterial { Shader = _backShader ??= new Shader { Code = BackShader } };
        _backdrop = new MeshInstance3D
        {
            Name = "Backdrop", Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = _backMat, Layers = FloorLayer,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _cam.AddChild(_backdrop);
    }

    /// <summary>
    /// The one shadow and the pixel budget follow the graphics preset; the picture follows the 亮度 setting like the
    /// world does. Nothing here may select a render pipeline the level does not use: no MSAA, the depth of the shadow
    /// atlas taken from the main view. No TAA either — measured with the pipeline counters of --portrait-stats: at 高
    /// (main view with TAA) and 低 (FXAA) the first open compiles no pipeline on the spot.
    /// </summary>
    private void ApplyQuality()
    {
        int q = Gfx.Quality;
        float bright = Mathf.Clamp(Game.I?.Cfg?.Brightness ?? 1f, 0.8f, 1.3f);
        var root = GetTree()?.Root;
        bool sh16 = root == null || root.PositionalShadowAtlas16Bits;
        bool fxaa = _ss < FxaaBelow;
        if (q == _quality && Mathf.IsEqualApprox(bright, _bright) && sh16 == _vp.PositionalShadowAtlas16Bits &&
            fxaa == (_vp.ScreenSpaceAA == Viewport.ScreenSpaceAAEnum.Fxaa)) return;
        _quality = q; _bright = bright;
        _vp.Msaa3D = Viewport.Msaa.Disabled;
        _vp.UseTaa = false;
        _vp.ScreenSpaceAA = fxaa ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        _vp.PositionalShadowAtlas16Bits = sh16;
        bool shadow = q >= 1;
        _key.ShadowEnabled = shadow;
        _vp.PositionalShadowAtlasSize = shadow ? 1024 : 0;
        _env.TonemapExposure = bright * bright;
        _env.GlowEnabled = true;
    }

    // ───────────────────────── frame ─────────────────────────

    private static readonly float[] RestYaws = { BaseYaw - SwayAmp, BaseYaw, BaseYaw + SwayAmp };
    private static readonly float[] RoundYaws = BuildRoundYaws(16);
    private static float[] BuildRoundYaws(int n) { var a = new float[n]; for (int i = 0; i < n; i++) a[i] = Mathf.Tau * i / n; return a; }
    private readonly List<Vector3> _work = new();

    /// <summary>
    /// Solves the camera for the measured figure, twice: for the resting view (the figure as large as the picture
    /// allows, feet on the foot line, head-room above) and for the figure turned any way (what the player sees while
    /// dragging: the lens backs off just enough that a long weapon never leaves the picture).
    /// </summary>
    private void Refit()
    {
        if (_model == null) return;
        if (_measureDirty) Measure();
        _fitDirty = false;
        float h = _height, floor = _floorY;
        Solve(RestYaws, true, out _restDist, out _restTarget);
        Solve(RoundYaws, false, out _roundDist, out _roundTarget);
        if (_roundDist < _restDist) { _roundDist = _restDist; _roundTarget = _restTarget; }
        GoalFor(_yaw);
        if (_snap) { _camDist = _goalDist; _camTarget = _goalTarget; _snap = _px.X <= 0; }

        // the set is laid out in units of the figure's height, so a scaled model is lit the same way
        _key.Position = new Vector3(-0.78f * h, floor + 1.50f * h, 1.20f * h);
        _key.Transform = new Transform3D(Basis.LookingAt(new Vector3(0f, floor + h * 0.50f, 0f) - _key.Position, Vector3.Up), _key.Position);
        _key.SpotRange = 6f * h + 4f;
        _rim.Transform = new Transform3D(Basis.LookingAt(-new Vector3(0.95f, 0.85f, -1.5f).Normalized(), Vector3.Up), Vector3.Zero);
        _kick.Transform = new Transform3D(Basis.LookingAt(-new Vector3(-1.35f, 0.35f, -1.0f).Normalized(), Vector3.Up), Vector3.Zero);
        _ember.Position = new Vector3(0.28f * h, floor + 0.03f * h, 0.62f * h);
        _ember.OmniRange = 1.9f * h;
        float disc = 2.5f * h;
        _floor.Position = new Vector3(0f, floor, 0f);
        _floor.Scale = new Vector3(disc, 1f, disc);
        // the contact shadow turns with the figure: it lies under the feet, wherever the stance puts them
        _blob.Position = new Vector3(_feet.X, floor + 0.004f * h, _feet.Y);
        _blob.Scale = new Vector3(0.56f * h, 1f, 0.42f * h);
        _motes.Position = new Vector3(0f, floor + 0.35f * h, 0.1f * h);
        _motes.Scale = Vector3.One * (h / 1.8f);
        PlaceCamera();
        Log.Info("portrait", $"camera for {_modelId}: figure {h:0.00} m, lens {_restDist:0.00} m away at {_restTarget.Y:0.00} m, slid {_restTarget.X:0.00} m; " +
            $"turned: {_roundDist:0.00} m; picture {_px.X}x{_px.Y} px");
    }

    /// <summary>Where the lens belongs for a figure turned by <paramref name="yaw"/> from its resting view.</summary>
    private void GoalFor(float yaw)
    {
        float w = Mathf.SmoothStep(0.10f, 0.85f, Mathf.Abs(yaw));
        _goalDist = Mathf.Lerp(_restDist, _roundDist, w);
        _goalTarget = _restTarget.Lerp(_roundTarget, w);
    }

    /// <summary>
    /// The lens distance and aim that hold every hull point, with the figure turned by each of <paramref name="yaws"/>.
    /// The body's axis starts in the middle of the picture. Height normally sets the size; when a weapon reaches so far
    /// to one side that the width would set it, the frame may slide toward the weapon just as far as it takes (at most
    /// until figure and weapon together are centred), so the figure stays as large as the picture allows.
    /// </summary>
    private void Solve(float[] yaws, bool maySlide, out float dist, out Vector3 target)
    {
        _work.Clear();
        foreach (float a in yaws)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            foreach (var p in _cloud) _work.Add(new Vector3(p.X * c + p.Z * s, p.Y, -p.X * s + p.Z * c));
        }
        float h = _height, floor = _floorY;
        float aspect = _px.Y > 0 ? _px.X / (float)_px.Y : 1f;
        float tanV = Mathf.Tan(Mathf.DegToRad(FovDeg) * 0.5f), tanH = tanV * aspect;
        float pitch = Mathf.DegToRad(PitchDeg);
        Vector3 fwd = new(0f, -Mathf.Sin(pitch), -Mathf.Cos(pitch)), up = new(0f, Mathf.Cos(pitch), -Mathf.Sin(pitch)), right = Vector3.Right;
        target = new Vector3(0f, floor + h * 0.5f, 0f);
        dist = h / (2f * tanV);
        float yLo = -1f + 2f * FootRoom, yHi = 1f - 2f * HeadRoom, xMax = 1f - 2f * SideRoom;
        for (int it = 0; it < 10; it++)
        {
            Vector3 eye = target - fwd * dist;
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var c in _work)
            {
                Vector3 d = c - eye;
                float z = Mathf.Max(0.05f, d.Dot(fwd));
                float nx = d.Dot(right) / (z * tanH), ny = d.Dot(up) / (z * tanV);
                x0 = MathF.Min(x0, nx); x1 = MathF.Max(x1, nx); y0 = MathF.Min(y0, ny); y1 = MathF.Max(y1, ny);
            }
            {
                // the floor under the body's axis counts as the lowest point
                Vector3 d = new Vector3(0f, floor, 0f) - eye;
                y0 = MathF.Min(y0, d.Dot(up) / (Mathf.Max(0.05f, d.Dot(fwd)) * tanV));
            }
            float s;
            if (maySlide)
            {
                s = Mathf.Clamp(MathF.Max((y1 - y0) / (yHi - yLo), (x1 - x0) * 0.5f / xMax), 0.5f, 2f);
                float slideLo = x1 - s * xMax, slideHi = x0 + s * xMax;
                float slide = slideLo >= slideHi ? (slideLo + slideHi) * 0.5f : MathF.Min(MathF.Max(0f, slideLo), slideHi);
                target.X += slide * dist * tanH;
            }
            else s = Mathf.Clamp(MathF.Max((y1 - y0) / (yHi - yLo), MathF.Max(MathF.Abs(x0), MathF.Abs(x1)) / xMax), 0.5f, 2f);
            dist *= s;
            // the feet stay on the foot line whatever limits the size
            target.Y += (y0 / s - yLo) * dist * tanV;
        }
    }

    private void PlaceCamera()
    {
        float pitch = Mathf.DegToRad(PitchDeg);
        Vector3 fwd = new(0f, -Mathf.Sin(pitch), -Mathf.Cos(pitch));
        _cam.Transform = new Transform3D(Basis.LookingAt(fwd, Vector3.Up), _camTarget - fwd * _camDist);
        _cam.Far = _camDist + 12f * _height;
        // the backdrop fills the lens behind everything
        float aspect = _px.Y > 0 ? _px.X / (float)_px.Y : 1f;
        float back = _camDist + 3.2f * _height;
        float tall = 2f * back * Mathf.Tan(Mathf.DegToRad(FovDeg) * 0.5f) * 1.04f;
        _backdrop.Transform = new Transform3D(Basis.Identity.Scaled(new Vector3(tall * aspect, tall, 1f)), new Vector3(0f, 0f, -back));
        _backMat.SetShaderParameter("aspect", aspect);
    }

    /// <summary>
    /// The viewport is rendered at the control's size in screen pixels (design size × the stretch of the window),
    /// supersampled for smooth edges: up to 2×2 pixels per screen pixel within a pixel budget for the preset, and the
    /// container scales the picture down (linear filter: an exact 2×2 box at the full factor).
    /// </summary>
    private void FitPixels()
    {
        if (!IsInsideTree()) return;
        Vector2 size = Size;
        if (size.X < 2f || size.Y < 2f) return;
        Transform2D xf = GetViewportTransform() * GetGlobalTransform();
        float kx = xf.X.Length() * _pxScale, ky = xf.Y.Length() * _pxScale;
        if (kx < 0.01f || ky < 0.01f) return;
        float screenPx = Mathf.Max(1f, size.X * kx * size.Y * ky);
        float budget = Gfx.Quality >= 1 ? BudgetPx : BudgetPxLow;
        float ss = Mathf.Clamp(Mathf.Sqrt(budget / screenPx), 1f, SuperMax);
        if (ss > SuperMax - 0.08f) ss = SuperMax;           // the whole factor where it nearly fits: an exact box filter
        kx *= ss; ky *= ss;
        var px = new Vector2I(Mathf.Clamp(Mathf.RoundToInt(size.X * kx), 16, MaxPx), Mathf.Clamp(Mathf.RoundToInt(size.Y * ky), 16, MaxPx));
        if (px == _px && size == _fitSize) return;
        _px = px; _fitSize = size; _ss = ss;
        _box.Position = Vector2.Zero;
        _box.Scale = new Vector2(size.X / px.X, size.Y / px.Y);
        _box.Size = px;                 // stretch: the SubViewport takes exactly this many pixels
        _fitDirty = true;
    }

    private void Activate(bool on)
    {
        on = on && _model != null;
        if (_vp == null) return;
        var want = on ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (_vp.RenderTargetUpdateMode != want) _vp.RenderTargetUpdateMode = want;
        if (_box != null && _box.Visible != (_model != null)) _box.Visible = _model != null;
        if (_model != null && IsInstanceValid(_model)) _model.SetAsleep(!on);
        if (on == _active) return;
        _active = on;
        if (!on) _dragging = false;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged || what == NotificationEnterTree)
        {
            bool vis = IsInsideTree() && IsVisibleInTree();
            Activate(vis);
            if (!vis) { _dragging = false; _lastTickUsec = 0; }
            else if (_stats) _pipeBase = Pipelines();       // what opening compiles shows in the next stats line
            if (vis && (_hero != null || _class >= 0)) _syncPending = true;
        }
        else if (what == NotificationExitTree) Activate(false);
        else if (what == NotificationResized) FitPixels();
        else if (what == NotificationPredelete)
        {
            if (_hero != null) { _hero.Changed -= OnHeroChanged; _hero = null; }
        }
    }

    public override void _Process(double delta)
    {
        if (_vp == null || !IsVisibleInTree()) return;
        try { Tick(Mathf.Min((float)delta, 0.1f)); }
        catch (Exception e)
        {
            _syncPending = false; _measureDirty = false; _fitDirty = false;
            Log.Error("portrait", "portrait frame failed", e);
        }
    }

    private void Tick(float dt)
    {
        FitPixels();
        if (_syncPending) SyncNow();
        if (_model != null) Animate(dt);
        Stats(dt);
    }

    private void Animate(float dt)
    {
        if (!_active) Activate(true);
        ApplyQuality();
        if (_measureDirty || _fitDirty) Refit();

        _t += dt;
        if (!_dragging)
        {
            if (_hold > 0f) _hold -= dt;
            else if (_yaw != 0f)
            {
                _yaw = Mathf.Lerp(_yaw, 0f, 1f - Mathf.Exp(-ReturnRate * dt));
                if (Mathf.Abs(_yaw) < 0.002f) _yaw = 0f;
            }
        }
        // the slow sway gives way while the player holds the figure turned
        float calm = 1f - Mathf.Clamp(Mathf.Abs(_yaw) / 0.5f, 0f, 1f);
        _pivot.Rotation = new Vector3(0f, BaseYaw + _yaw + SwayAmp * calm * Mathf.Sin(_t * SwayRate), 0f);

        // turned by the player, the lens backs off as far as the weapon needs; it comes forward again with the figure
        GoalFor(_yaw);
        float k = 1f - Mathf.Exp(-9f * dt);
        if (!Mathf.IsEqualApprox(_camDist, _goalDist) || !_camTarget.IsEqualApprox(_goalTarget))
        {
            _camDist = Mathf.Lerp(_camDist, _goalDist, k);
            _camTarget = _camTarget.Lerp(_goalTarget, k);
            if (Mathf.Abs(_camDist - _goalDist) < 0.002f) { _camDist = _goalDist; _camTarget = _goalTarget; }
            PlaceCamera();
        }
        // the ember breathes
        _ember.LightEnergy = 0.8f * (1f + 0.10f * Mathf.Sin(_t * 6.1f) * Mathf.Sin(_t * 2.3f) + 0.05f * Mathf.Sin(_t * 13.7f + 1.1f));
    }

    // ───────────────────────── measurement ─────────────────────────

    private bool _stats, _statsAsked, _off, _offAsked, _forceAsked;
    private float _statsT, _pxScale = 1f, _forceScale = 1f;
    private string _forceId;
    private double _cpu, _gpu, _mainCpu, _mainGpu, _worstMs;
    private int _statsN;
    private ulong _lastTickUsec;

    /// <summary>
    /// Test switches. --portrait-stats: every two seconds, what the portrait's viewport costs to render (the renderer's
    /// own CPU and GPU timers, next to those of the main picture), the longest frame since the last line (the first
    /// line after the owner opens holds the frame in which the model was built and first drawn) and a pose signature
    /// that changes while the idle plays. --portrait-off: nothing is put on the stage (the same lines without a
    /// portrait, for comparison). --portrait-scale=f: f times the pixels (what a larger screen would ask for).
    /// </summary>
    private void Stats(float dt)
    {
        if (!_statsAsked)
        {
            _statsAsked = true;
            _stats = Game.I != null && Game.I.Arg("portrait-stats");
            // --portrait-scale=2.4: render as many pixels as a 4K screen would ask for (cost measurement on a smaller screen)
            if (Game.I != null && float.TryParse(Game.I.ArgValue("portrait-scale"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ps))
                _pxScale = Mathf.Clamp(ps, 0.25f, 4f);
            if (_stats)
            {
                RenderingServer.ViewportSetMeasureRenderTime(_vp.GetViewportRid(), true);
                RenderingServer.ViewportSetMeasureRenderTime(GetTree().Root.GetViewportRid(), true);
            }
        }
        if (!_stats) return;
        if (_pipeBase == null) _pipeBase = Pipelines();
        ulong now = Time.GetTicksUsec();
        if (_lastTickUsec != 0) _worstMs = Math.Max(_worstMs, (now - _lastTickUsec) / 1000.0);
        _lastTickUsec = now;
        Rid mine = _vp.GetViewportRid(), main = GetTree().Root.GetViewportRid();
        if (_model != null) { _cpu += RenderingServer.ViewportGetMeasuredRenderTimeCpu(mine); _gpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(mine); }
        _mainCpu += RenderingServer.ViewportGetMeasuredRenderTimeCpu(main); _mainGpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(main);
        _statsN++;
        _statsT += dt;
        if (_statsT < 2f) return;
        string what = _model != null
            ? $"picture {_px.X}x{_px.Y} px ({_ss:0.00}x per axis), msaa {_vp.Msaa3D} taa {_vp.UseTaa} ssaa {_vp.ScreenSpaceAA} shadow16 {_vp.PositionalShadowAtlas16Bits}, shadow {_key.ShadowEnabled}; render cpu {_cpu / _statsN:0.00} ms gpu {_gpu / _statsN:0.00} ms; " +
              $"clip '{_model.Current}' playing={_model.Playing} pose {_model.PoseSignature():0.0000} yaw {_yaw:0.00}"
            : "no portrait on the stage";
        long[] pipe = Pipelines();
        string compiled = $"pipelines compiled draw +{pipe[0] - _pipeBase[0]} specialization +{pipe[1] - _pipeBase[1]} surface +{pipe[2] - _pipeBase[2]} mesh +{pipe[3] - _pipeBase[3]} canvas +{pipe[4] - _pipeBase[4]}";
        _pipeBase = pipe;
        Log.Info("portrait", $"stats: {_statsN} frames = {_statsN / _statsT:0} fps, longest frame {_worstMs:0.0} ms, main picture cpu {_mainCpu / _statsN:0.00} ms gpu {_mainGpu / _statsN:0.00} ms; {what}; {compiled}");
        _statsT = 0f; _statsN = 0; _cpu = _gpu = _mainCpu = _mainGpu = _worstMs = 0;
    }

    private long[] _pipeBase;

    /// <summary>
    /// The renderer's running counts of pipeline compilations (draw = compiled on the spot because a draw needed it:
    /// those are the stalls; specialization/surface/mesh = compiled ahead on worker threads; canvas = 2D).
    /// </summary>
    private static long[] Pipelines() => new[]
    {
        (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsDraw),
        (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsSpecialization),
        (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsSurface),
        (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsMesh),
        (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsCanvas),
    };

    // ───────────────────────── input ─────────────────────────

    public override void _GuiInput(InputEvent e)
    {
        if (!AllowDrag || _model == null) return;
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed) { _dragging = true; AcceptEvent(); }
            else if (_dragging) { _dragging = false; _hold = ReturnDelay; AcceptEvent(); }
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            if ((mm.ButtonMask & MouseButtonMask.Left) == 0) { _dragging = false; _hold = ReturnDelay; return; }     // released somewhere we did not see
            _yaw = Mathf.Wrap(_yaw + mm.Relative.X * DragRadPerPx, -Mathf.Pi, Mathf.Pi);
            _hold = ReturnDelay;
            AcceptEvent();
        }
    }
}
