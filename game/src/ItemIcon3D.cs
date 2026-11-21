using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// Item icons rendered from the item's own 3D model (CONTRACT §7): a consistent three-quarter hero shot, a warm key,
/// a cool fill and a rim light in the colour of the rarity, on a transparent background.
///
/// <see cref="Get"/> answers at once with a texture that the caller keeps: it shows the old raster icon until the
/// render is ready and then turns into the 3D picture in place (an AtlasTexture whose atlas is swapped), so no UI code
/// waits or rebuilds. Renders happen over frames on a few offscreen SubViewports (one model each per frame at most, a
/// time budget per frame, models read on a loader thread); finished pictures are kept on disk in user://icon3d/ and
/// read back on later runs. Cached per (model id, size, rarity). Null when the item has no model file: the caller
/// keeps the raster icon.
/// </summary>
public partial class ItemIcon3D : Node
{
    private const int Version = 9;      // bump to re-render every cached picture
    private const int SuperSample = 2;
    private const int LaneCount = 3;
    private const double FrameBudgetMs = 3.0;
    private const string DiskDir = "user://icon3d/";

    private sealed class Job
    {
        public string Key, Id, Disk;
        public int Size;
        public Rarity Rarity;
        public AtlasTexture Tex;
        public int Tries;
    }

    private sealed class Lane
    {
        public SubViewport Vp;
        public Camera3D Cam;
        public Node3D Slot;
        public DirectionalLight3D Rim;
        public Job Job;
        public ulong ReadyAt;
        public Node3D Model;
    }

    private static readonly Dictionary<string, AtlasTexture> Cache = new();
    private static readonly LinkedList<Job> Queue = new();
    private static ItemIcon3D _node;
    private static ImageTexture _blank;
    private static bool? _off;
    private Lane[] _lanes;

    // statistics (perf report, test suite)
    public static int Requested, Rendered, ReadFromDisk, Failed;
    public static double RenderMsTotal, WorstFrameMs;
    public static int Pending => Queue.Count + (_node?._lanes?.Count(l => l.Job != null) ?? 0);

    public static bool Disabled => _off ??= GearCatalog.Off || (Game.I?.Arg("icon3d-off") ?? false) || DisplayServer.GetName() == "headless";

    /// <summary>The 3D icon of an item at about <paramref name="size"/> pixels, or null when the item has no model file.</summary>
    public static Texture2D Get(Item it, int size = 128)
    {
        if (it == null || Disabled) return null;
        string id;
        try { id = GearCatalog.ModelIdFor(it, Game.I?.Hero?.Class ?? ClassId.Emberblade); }
        catch (Exception e) { Log.Error("icon3d", "model lookup failed", e); return null; }
        if (id == null) return null;
        int s = size <= 64 ? 64 : size <= 128 ? 128 : 256;
        string key = $"{id}|{s}|{(int)it.Rarity}";
        if (Cache.TryGetValue(key, out var t)) return t;
        Texture2D first = null;
        try { first = Icons.ForItemRaster(it); } catch (Exception) { first = null; }
        first ??= Blank;
        t = new AtlasTexture { Atlas = first, Region = new Rect2(Vector2.Zero, first.GetSize()) };
        Icons.MarkRaster(t);
        Cache[key] = t;
        Requested++;
        Queue.AddLast(new Job { Key = key, Id = id, Size = s, Rarity = it.Rarity, Tex = t, Disk = DiskPath(key, id) });
        EnsureNode();
        return t;
    }

    /// <summary>Forget the pictures in memory (and on disk with <paramref name="disk"/>): the next Get renders again.</summary>
    public static void Flush(bool disk = false)
    {
        Cache.Clear();
        Queue.Clear();
        _off = null;
        if (!disk) return;
        var d = DirAccess.Open(DiskDir);
        if (d == null) return;
        foreach (var f in d.GetFiles()) d.Remove(f);
    }

    private static Texture2D Blank
    {
        get
        {
            if (_blank != null) return _blank;
            var img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
            img.Fill(new Color(0, 0, 0, 0));
            return _blank = ImageTexture.CreateFromImage(img);
        }
    }

    private static string DiskPath(string key, string id)
    {
        string file = GearCatalog.PathOf(id);
        long mt = 0;
        try { if (file.Length > 0) mt = (long)FileAccess.GetModifiedTime(file.StartsWith("res://") && ResourceLoader.Exists(file) && FileAccess.FileExists(file + ".import") ? file + ".import" : file); }
        catch (Exception) { mt = 0; }
        string ver = ProjectSettings.GetSetting("application/config/version", "").AsString();
        string stamp = Fnv($"{Version}|{ver}|{GearCatalog.ManifestStamp}|{file}|{mt}|{(file.Length == 0 ? "ph" : "")}");
        var safe = new System.Text.StringBuilder();
        foreach (char c in key) safe.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '-');
        return $"{DiskDir}{safe}_{stamp}.png";
    }

    private static string Fnv(string s)
    {
        uint h = 2166136261;
        foreach (char c in s) { h ^= c; h *= 16777619; }
        return h.ToString("x8");
    }

    private static void EnsureNode()
    {
        if (_node != null && IsInstanceValid(_node)) return;
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null) return;
        _node = new ItemIcon3D { Name = "ItemIcon3D", ProcessMode = ProcessModeEnum.Always };
        tree.Root.CallDeferred(Node.MethodName.AddChild, _node);
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(DiskDir));
        _lanes = new Lane[LaneCount];
        for (int i = 0; i < LaneCount; i++) _lanes[i] = BuildLane(i);
    }

    public override void _ExitTree() { if (_node == this) _node = null; }

    private Lane BuildLane(int i)
    {
        var vp = new SubViewport
        {
            Name = "Lane" + i, Size = new Vector2I(256 * SuperSample, 256 * SuperSample), TransparentBg = true, OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Msaa4X, ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
        };
        AddChild(vp);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor, AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.46f, 0.45f, 0.5f), AmbientLightEnergy = 0.55f, TonemapMode = Godot.Environment.ToneMapper.Aces, TonemapExposure = 1.05f,
        };
        vp.AddChild(new WorldEnvironment { Environment = env });
        var cam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 1f, Near = 0.01f, Far = 40f, Position = new Vector3(0, 0, 10) };
        vp.AddChild(cam);
        DirectionalLight3D Light(Vector3 from, Color c, float e)
        {
            var l = new DirectionalLight3D { LightColor = c, LightEnergy = e, ShadowEnabled = false };
            l.Basis = Basis.LookingAt(-from.Normalized(), Mathf.Abs(from.Normalized().Y) > 0.95f ? Vector3.Forward : Vector3.Up);
            vp.AddChild(l);
            return l;
        }
        Light(new Vector3(-1f, 1.3f, 1.1f), new Color(1f, 0.92f, 0.82f), 1.7f);        // warm key, upper front left
        Light(new Vector3(1.2f, 0.2f, 0.9f), new Color(0.7f, 0.78f, 0.95f), 0.45f);    // cool fill, right
        var rim = Light(new Vector3(0.6f, 0.7f, -1.2f), Colors.White, 2.2f);           // rim from behind, in the rarity's colour
        var slot = new Node3D { Name = "Slot" };
        vp.AddChild(slot);
        return new Lane { Vp = vp, Cam = cam, Slot = slot, Rim = rim };
    }

    public override void _Process(double delta)
    {
        if (_lanes == null) return;
        ulong t0 = Time.GetTicksUsec();
        double Ms() => (Time.GetTicksUsec() - t0) / 1000.0;
        ulong frame = Engine.GetProcessFrames();

        // 1. finished renders: read back, scale down, keep, save
        foreach (var lane in _lanes)
        {
            if (lane.Job == null || frame < lane.ReadyAt || Ms() >= FrameBudgetMs) continue;      // one read-back at least, more while the budget lasts
            Finish(lane);
        }
        // 2. pictures kept on disk from an earlier run
        int scanned = 0, n = Queue.Count;
        for (var node = Queue.First; node != null && scanned < n && Ms() < FrameBudgetMs;)
        {
            var next = node.Next;
            var job = node.Value;
            scanned++;
            if (FileAccess.FileExists(job.Disk))
            {
                Queue.Remove(node);
                var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(job.Disk));
                if (img != null && !img.IsEmpty()) { Apply(job, img); ReadFromDisk++; }
                else
                {
                    // unreadable (a picture still being written, a damaged file): drop it and render afresh
                    DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(job.Disk));
                    Queue.AddLast(job);
                }
            }
            node = next;
            if (scanned > 24) break;            // look a little way down the queue each frame, not through all of it
        }
        // 3. new renders on idle lanes
        foreach (var lane in _lanes)
        {
            if (lane.Job != null || Queue.Count == 0 || Ms() >= FrameBudgetMs) continue;
            int tries = Queue.Count;
            while (tries-- > 0 && Queue.Count > 0)
            {
                var job = Queue.First.Value;
                Queue.RemoveFirst();
                if (FileAccess.FileExists(job.Disk)) { Queue.AddFirst(job); break; }
                if (!GearCatalog.Prefetch(job.Id)) { Queue.AddLast(job); continue; }       // still loading on the loader thread
                Start(lane, job);
                break;
            }
        }
        double ms = Ms();
        if (ms > WorstFrameMs) WorstFrameMs = ms;
        RenderMsTotal += ms;
    }

    private void Start(Lane lane, Job job)
    {
        ulong t0 = Time.GetTicksUsec();
        Node3D model = null;
        try { model = GearCatalog.Instantiate(job.Id); }
        catch (Exception e) { Log.Error("icon3d", $"'{job.Id}' could not be instanced", e); }
        if (model == null)
        {
            if (++job.Tries < 2) Queue.AddLast(job); else { Failed++; Log.Warn("icon3d", $"no model for '{job.Id}': the raster icon stays"); }
            return;
        }
        foreach (var ap in GearCatalog.Walk(model).OfType<AnimationPlayer>().ToList()) ap.QueueFree();
        lane.Slot.Basis = Pose(job.Id) * Chunky(job.Id, model);
        lane.Slot.AddChild(model);
        lane.Model = model;
        // frame the figure: bounds in camera space (the camera looks down -Z from +Z)
        var box = new Aabb(); bool first = true;
        foreach (var vi in GearCatalog.Walk(model).OfType<VisualInstance3D>())
        {
            var a = vi.GetAabb();
            var xf = vi.GlobalTransform;
            for (int i = 0; i < 8; i++)
            {
                var q = xf * a.GetEndpoint(i);
                if (first) { box = new Aabb(q, Vector3.Zero); first = false; } else box = box.Expand(q);
            }
        }
        if (first) box = new Aabb(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.One);
        // a pair of gloves in the bind pose is two hands at arm's length: show the right one
        if (job.Id.Contains("_gloves_") && OneSide(model, out var side)) box = side;
        // weapons: the striking end fills the square (the handle may run off the corner); short pieces whole
        var (frac, fill) = Framing(job.Id);
        if (frac > 0f && HeadBox(model, frac, out var head)) box = head;
        var c = box.GetCenter();
        float span = Mathf.Max(Mathf.Max(box.Size.X, box.Size.Y), 0.02f);
        lane.Cam.Size = span / fill;
        lane.Cam.Position = new Vector3(c.X, c.Y, box.End.Z + 5f);
        lane.Cam.Far = box.Size.Z + 10f;
        lane.Rim.LightColor = RimColor(job.Rarity);
        lane.Rim.LightEnergy = job.Rarity >= Rarity.Legendary ? 2.8f : 2.2f;
        int px = Math.Min(512, job.Size * SuperSample);
        if (lane.Vp.Size.X != px) lane.Vp.Size = new Vector2I(px, px);
        lane.Vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        lane.Job = job;
        lane.ReadyAt = Engine.GetProcessFrames() + 2;     // drawn at the end of this frame; read back two frames on
        double ms = (Time.GetTicksUsec() - t0) / 1000.0;
        if (ms > 20.0) Log.Info("icon3d", $"slow set-up of '{job.Id}': {ms:0.0} ms (first use of the file)");
    }

    private void Finish(Lane lane)
    {
        var job = lane.Job;
        lane.Job = null;
        ulong t0 = Time.GetTicksUsec();
        try
        {
            var img = lane.Vp.GetTexture().GetImage();
            if (img == null || img.IsEmpty()) { Failed++; return; }
            if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);
            if (img.GetWidth() != job.Size) img.Resize(job.Size, job.Size, Image.Interpolation.Lanczos);
            Silhouette(img, job.Rarity);
            Apply(job, img);
            Rendered++;
            double ms = (Time.GetTicksUsec() - t0) / 1000.0;
            if (ms > 15.0) Log.Info("icon3d", $"slow read-back of '{job.Id}': {ms:0.0} ms");
            var copy = (Image)img.Duplicate();
            string path = ProjectSettings.GlobalizePath(job.Disk);
            System.Threading.Tasks.Task.Run(() => { try { copy.SavePng(path); } catch (Exception) { } });
        }
        catch (Exception e) { Failed++; Log.Error("icon3d", $"reading back '{job.Id}' failed", e); }
        finally
        {
            if (lane.Model != null && IsInstanceValid(lane.Model)) { lane.Slot.RemoveChild(lane.Model); lane.Model.QueueFree(); }
            lane.Model = null;
        }
    }

    private static void Apply(Job job, Image img)
    {
        if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);
        if (!img.HasMipmaps()) img.GenerateMipmaps();
        var tex = ImageTexture.CreateFromImage(img);
        job.Tex.Atlas = tex;
        job.Tex.Region = new Rect2(0, 0, img.GetWidth(), img.GetHeight());
    }

    /// <summary>
    /// A long thin weapon (a sword is twenty times longer than wide) reads as a hairline in a 64 px cell: its width and
    /// thickness are drawn up to 1.8× wider in the icon (never in the world), keeping its length.
    /// </summary>
    private static Basis Chunky(string id, Node3D model)
    {
        string s = id.StartsWith("ph_") ? id[3..] : id;
        if (!s.StartsWith("wpn_") || s.StartsWith("wpn_shield")) return Basis.Identity;
        var size = GearCatalog.LocalBounds(model, null).Size;          // weapon axes: Z up the weapon
        float across = Mathf.Max(Mathf.Max(size.X, size.Y), 0.005f);
        string bb = s.Length > 4 ? s[4..] : "";
        float cap = bb.StartsWith("sword") ? 1.55f : bb.StartsWith("bow") ? 1.8f : 1.3f;
        float k = bb.StartsWith("sword") || bb.StartsWith("bow") ? cap : Mathf.Clamp(size.Z / across / 8f, 1f, cap);     // a guard or a bow's curve must not cancel the widening
        return Basis.FromScale(new Vector3(k, k, 1f));
    }

    /// <summary>
    /// Per base: which share of the weapon's length (from its tip down) is framed, and how much of the square it fills.
    /// Long weapons show their striking end large (blade, head, bit, scythe blade); short pieces and off-hands stay whole.
    /// (frac 0 = frame the whole model by its bounds.)
    /// </summary>
    private static (float frac, float fill) Framing(string id)
    {
        string s = id.StartsWith("ph_") ? id[3..] : id;
        if (!s.StartsWith("wpn_")) return s.StartsWith("jwl_") || s.StartsWith("gem_") ? (1f, 0.8f) : (0f, 0.88f);
        int u = s.LastIndexOf("_v", StringComparison.Ordinal);
        string b = u > 4 ? s[4..u] : "";
        return b switch
        {
            "sword1" => (0.92f, 0.72f), "sword2" => (0.7f, 0.72f),
            "axe1" => (0.6f, 0.7f), "axe2" => (0.42f, 0.7f),
            "mace1" => (0.55f, 0.7f), "mace2" => (0.4f, 0.7f), "flail" => (0.72f, 0.72f),
            "scythe1" => (0.62f, 0.7f), "scythe2" => (0.3f, 0.7f),
            "staff" => (0.3f, 0.7f), "polearm" => (0.33f, 0.7f),
            "bow" => (1f, 0.74f),
            _ => (1f, 0.8f),          // dagger wand fist sceptre crossbow and the off-hands: whole
        };
    }

    /// <summary>Camera-space bounds of the vertices in the top <paramref name="frac"/> of the model's length (model +Z = up the weapon).</summary>
    private static bool HeadBox(Node3D model, float frac, out Aabb box)
    {
        box = new Aabb();
        var inv = model.GlobalTransform.AffineInverse();
        var pts = new List<(Vector3 cam, float z)>();
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var mi in GearCatalog.Walk(model).OfType<MeshInstance3D>())
        {
            if (mi.Mesh == null || !mi.Visible) continue;
            var xf = mi.GlobalTransform;
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                foreach (var v in mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                {
                    var w = xf * v;
                    float z = (inv * w).Z;
                    lo = Mathf.Min(lo, z); hi = Mathf.Max(hi, z);
                    pts.Add((w, z));
                }
        }
        if (pts.Count == 0) return false;
        float cut = hi - (hi - lo) * Mathf.Clamp(frac, 0.05f, 1f) - 1e-4f;
        bool first = true;
        foreach (var (w, z) in pts)
        {
            if (z < cut) continue;
            if (first) { box = new Aabb(w, Vector3.Zero); first = false; } else box = box.Expand(w);
        }
        return !first;
    }

    /// <summary>
    /// A thin dark outline round the shape and a faint back-glow in the rarity's colour behind it, so the silhouette
    /// reads on any slot background.
    /// </summary>
    private static void Silhouette(Image img, Rarity r)
    {
        int w = img.GetWidth(), h = img.GetHeight();
        var a = new float[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) a[y * w + x] = img.GetPixel(x, y).A;
        int ro = Math.Max(1, w / 64), rg = Math.Max(2, w / 14);
        // glow: box blur of the alpha (two passes, separable)
        float[] Blur(float[] src, int rad)
        {
            var t = new float[w * h]; var o = new float[w * h];
            for (int y = 0; y < h; y++) { float sum = 0; for (int x = -rad; x < w + rad; x++) { if (x + rad < w && x + rad >= 0) sum += src[y * w + x + rad]; if (x - rad - 1 >= 0 && x - rad - 1 < w) sum -= src[y * w + x - rad - 1]; if (x >= 0 && x < w) t[y * w + x] = sum / (2 * rad + 1); } }
            for (int x = 0; x < w; x++) { float sum = 0; for (int y = -rad; y < h + rad; y++) { if (y + rad < h && y + rad >= 0) sum += t[(y + rad) * w + x]; if (y - rad - 1 >= 0 && y - rad - 1 < h) sum -= t[(y - rad - 1) * w + x]; if (y >= 0 && y < h) o[y * w + x] = sum / (2 * rad + 1); } }
            return o;
        }
        var glow = Blur(a, rg);
        // a dark model (black wood, blued steel) is lifted so it does not sink into the slot
        double lum = 0; int nOpaque = 0;
        for (int i = 0; i < a.Length; i++) if (a[i] > 0.5f) { var q = img.GetPixel(i % w, i / w); lum += 0.3 * q.R + 0.59 * q.G + 0.11 * q.B; nOpaque++; }
        if (nOpaque > 0 && lum / nOpaque < 0.2)
        {
            float gain = (float)Math.Min(1.8, 0.2 / Math.Max(0.02, lum / nOpaque));
            for (int i = 0; i < a.Length; i++) if (a[i] > 0f) { var q = img.GetPixel(i % w, i / w); img.SetPixel(i % w, i / w, new Color(q.R * gain, q.G * gain, q.B * gain, q.A)); }
        }
        var gc = r == Rarity.Common ? new Color(0.85f, 0.75f, 0.6f) : Palette.Rarity(r);
        var dark = new Color(0.035f, 0.028f, 0.024f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (a[i] >= 0.999f) continue;
                // outline: the strongest alpha within ro pixels
                float near = 0f;
                for (int dy = -ro; dy <= ro && near < 1f; dy++)
                    for (int dx = -ro; dx <= ro; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        near = Mathf.Max(near, a[yy * w + xx]);
                    }
                float oa = Mathf.Clamp(near * 1.6f, 0f, 1f) * 0.92f;
                float ga = Mathf.Clamp(glow[i] * 0.9f, 0f, 0.55f);
                // under = outline over glow; result = object over under
                float ua = oa + ga * (1f - oa);
                Color under = ua <= 0f ? new Color(0, 0, 0, 0) : new Color((dark.R * oa + gc.R * ga * (1f - oa)) / ua, (dark.G * oa + gc.G * ga * (1f - oa)) / ua, (dark.B * oa + gc.B * ga * (1f - oa)) / ua, ua);
                var p = img.GetPixel(x, y);
                float outA = p.A + ua * (1f - p.A);
                if (outA <= 0f) continue;
                img.SetPixel(x, y, new Color((p.R * p.A + under.R * ua * (1f - p.A)) / outA, (p.G * p.A + under.G * ua * (1f - p.A)) / outA, (p.B * p.A + under.B * ua * (1f - p.A)) / outA, outA));
            }
    }

    /// <summary>Bounds (camera space) of the vertices on the model's right side (−X: the figure faces +Z).</summary>
    private static bool OneSide(Node3D model, out Aabb box)
    {
        box = new Aabb(); bool first = true;
        foreach (var mi in GearCatalog.Walk(model).OfType<MeshInstance3D>())
        {
            if (mi.Mesh == null) continue;
            var xf = mi.GlobalTransform;
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                foreach (var v in mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                {
                    if (v.X > 0f) continue;
                    var q = xf * v;
                    if (first) { box = new Aabb(q, Vector3.Zero); first = false; } else box = box.Expand(q);
                }
        }
        return !first;
    }

    private static Color RimColor(Rarity r) => r switch
    {
        Rarity.Common => new Color(0.75f, 0.8f, 0.9f),
        _ => Palette.Rarity(r).Lerp(Colors.White, 0.15f),
    };

    /// <summary>The hero shot of each kind of model: blades on the diagonal, armour three-quarter front, shields face on.</summary>
    private static Basis Pose(string id)
    {
        Basis R(Vector3 axis, float deg) => new(axis, Mathf.DegToRad(deg));
        string s = id.StartsWith("ph_") ? id[3..] : id;
        if (s.StartsWith("arm_")) return R(Vector3.Up, -28f) * R(Vector3.Right, 6f);
        return Display(s) * GearGrips.G2B.Basis.Inverse();         // weapon and jewel files are Z-up: stand them up first
    }

    /// <summary>The pose of a weapon or jewel stood up (+Y up the weapon).</summary>
    private static Basis Display(string s)
    {
        Basis R(Vector3 axis, float deg) => new(axis, Mathf.DegToRad(deg));
        if (s.StartsWith("jwl_") || s.StartsWith("gem_")) return R(Vector3.Up, 25f) * R(Vector3.Right, -25f);
        if (s.StartsWith("wpn_shield")) return R(Vector3.Up, 180f + 24f) * R(Vector3.Right, 6f);
        if (s.StartsWith("wpn_crossbow")) return R(Vector3.Back, 18f) * R(Vector3.Up, -35f);
        if (s.StartsWith("wpn_focus") || s.StartsWith("wpn_grimoire") || s.StartsWith("wpn_lantern") || s.StartsWith("wpn_totem") || s.StartsWith("wpn_fist"))
            return R(Vector3.Up, -30f) * R(Vector3.Right, 8f);
        return R(Vector3.Back, -38f) * R(Vector3.Right, 16f) * R(Vector3.Up, 18f);
    }
}
