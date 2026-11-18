using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// A small public event in a region's overworld (two or three per visit). The level ticks every event; the active
/// one supplies the objective text and progress bar. Success drops a reward chest and counts toward renown.
/// </summary>
public abstract class WorldEvent
{
    public Level L;
    public Vector2 Pos;
    public bool Active, Done;
    public virtual string Text => "";
    public virtual float Progress => 0f;
    protected readonly List<Monster> Mobs = new();
    protected Interactable Obj;
    protected Random Rng => L.Rand;

    public abstract void Setup();
    public virtual void Tick(float dt) { }

    protected int Alive()
    {
        int n = 0;
        foreach (var m in Mobs) if (m != null && GodotObject.IsInstanceValid(m) && !m.Dead) n++;
        return n;
    }

    protected Vector2 Around(float rMin, float rMax) => L.Grid.RandomWalkNear(Pos, rMin, rMax, L.Rand);

    protected void Wave(int packs, int size)
    {
        for (int i = 0; i < packs; i++)
        {
            var list = Populate.Pack(L, Around(7f, 10f), size);
            foreach (var m in list) { m.ForceAggro(); Mobs.Add(m); Fx.Burst(m.GlobalPosition + Vector3.Up * 0.3f, Element.Shadow, 1.4f, 0.4f); }
        }
        Snd.At("elite_spawn", World.To3(Pos), 0.7f);
    }

    protected void Begin(string toast)
    {
        Active = true;
        Snd.Ui("event_start");
        Game.I?.Hud?.Toast(toast, Palette.Gold);
        L.MarkDirty();
        Log.Info("event", $"{GetType().Name} started in {L.AreaId}");
    }

    protected void Succeed(int chestQuality, string toast)
    {
        Active = false; Done = true;
        Snd.Ui("event_complete");
        Game.I?.Hud?.Toast(toast, Palette.Gold);
        if (Obj != null) L.RemoveMarker(Obj);
        L.RewardChest(L.Grid.RandomWalkNear(Pos, 1.6f, 2.6f, L.Rand), chestQuality);
        L.MarkDirty();
        Log.Info("event", $"{GetType().Name} completed in {L.AreaId}");
        World.Emit("event", L.Region?.Id ?? "");
    }

    protected void Fail(string toast)
    {
        Active = false; Done = true;
        Snd.Ui("ui_error");
        Game.I?.Hud?.Toast(toast, Palette.Bad);
        if (Obj != null) L.RemoveMarker(Obj);
        L.MarkDirty();
        Log.Info("event", $"{GetType().Name} failed in {L.AreaId}");
    }
}

/// <summary>守火：rekindle a dying brazier and hold it through three waves before its light runs out.</summary>
public sealed class BrazierEvent : WorldEvent
{
    private int _wave;
    private float _light = 1f, _gap;
    private const int Waves = 3;
    public override string Text => $"守住火盆（第 {Math.Min(_wave, Waves)}/{Waves} 波）";
    public override float Progress => _light;

    public override void Setup()
    {
        Obj = WorldObjects.DyingBrazier(Pos, _ => Start());
        L.AddMarker("event", "將熄的火盆", Pos, Obj, true);
    }

    private void Start()
    {
        if (Active || Done) return;
        Obj.CanUse = false;
        var fire = Fx.Fire(0.8f, true);
        if (fire != null) { fire.Position = new Vector3(0, 0.8f, 0); Obj.AddChild(fire); }
        Begin("火盆重燃——寂影循光而來");
        _wave = 1; _gap = 0f;
        Wave(2, 4);
    }

    public override void Tick(float dt)
    {
        if (!Active) return;
        _light -= dt / 95f;                               // the fire burns down while the fight lasts
        if (Alive() == 0)
        {
            _gap += dt;
            if (_gap < 1.5f) return;
            _gap = 0f;
            if (_wave >= Waves) { Succeed(1, "火光守住了"); return; }
            _wave++;
            _light = Mathf.Min(1f, _light + 0.2f);
            Wave(_wave == Waves ? 3 : 2, 4 + _wave);
            L.MarkDirty();
        }
        else if (_light <= 0f)
        {
            Fail("火熄了。黑暗重新合攏");
        }
    }
}

/// <summary>受詛的寶箱：opening it calls waves for forty seconds; survive and clear them for the real reward.</summary>
public sealed class CursedChestEvent : WorldEvent
{
    private float _time, _next;
    private const float Duration = 40f;
    public override string Text => _time < Duration ? "撐過詛咒" : $"肅清殘敵（{Alive()}）";
    public override float Progress => Mathf.Clamp(_time / Duration, 0f, 1f);

    public override void Setup()
    {
        Obj = WorldObjects.CursedChest(Pos, _ => Start());
        L.AddMarker("event", "受詛的寶箱", Pos, Obj, true);
    }

    private void Start()
    {
        if (Active || Done) return;
        Obj.CanUse = false;
        Snd.At("chest_open", World.To3(Pos));
        Fx.Burst(World.To3(Pos, 0.6f), Element.Shadow, 4f, 1f);
        Begin("箱中空無一物——只有詛咒");
        _time = 0f; _next = 0f;
    }

    public override void Tick(float dt)
    {
        if (!Active) return;
        if (_time < Duration)
        {
            _time += dt; _next -= dt;
            if (_next <= 0f) { _next = 9f; Wave(1, 5 + (int)(_time / 14f)); }
            return;
        }
        if (Alive() == 0) Succeed(2, "詛咒散去，箱底現出真正的收藏");
    }
}

/// <summary>寂滅儀式：kill the chanting priests before the ritual completes, or face what they were calling.</summary>
public sealed class RitualEvent : WorldEvent
{
    private Node3D _circle; private MeshInstance3D _orb;
    private float _fill;
    private const float Duration = 34f;
    public override string Text => $"打斷寂滅儀式（祭司 {Alive()}）";
    public override float Progress => Mathf.Clamp(_fill / Duration, 0f, 1f);

    public override void Setup()
    {
        _circle = WorldObjects.RitualCircle(Pos, 3.2f);
        _orb = _circle?.GetNodeOrNull<MeshInstance3D>("orb");
        var def = Populate.CasterDef(L);
        int pack = L.NextPack();
        for (int i = 0; i < 4; i++)
        {
            Vector2 at = Pos + Vector2.Right.Rotated(i * Mathf.Tau / 4f + 0.4f) * 4.2f;
            var m = Populate.SpawnOne(L, def, i == 0 ? MonTier.Champion : MonTier.Normal, at, pack, i == 0 ? Populate.PickAffixes(1, L.Rand) : null);
            m.FaceDir(Pos - m.Pos2);
            Mobs.Add(m);
        }
        L.AddMarker("event", "寂滅儀式", Pos, this, true);
    }

    public override void Tick(float dt)
    {
        if (Done) return;
        var p = World.P;
        if (!Active)
        {
            if (p == null || p.Dead || p.Pos2.DistanceTo(Pos) > 15f) return;
            Begin("祭司們正在喚醒什麼——阻止他們");
            return;
        }
        _fill += dt;
        if (_orb != null && GodotObject.IsInstanceValid(_orb)) _orb.Scale = Vector3.One * (0.3f + 1.5f * Progress);
        if (Alive() == 0)
        {
            Cleanup();
            L.RemoveMarker(this);
            Succeed(1, "儀式中斷，餘音散入灰暮");
        }
        else if (_fill >= Duration)
        {
            Cleanup();
            L.RemoveMarker(this);
            Fx.Burst(World.To3(Pos, 0.8f), Element.Shadow, 6f, 1.6f);
            var boss = Populate.Elite(L, Pos, 3, false);
            boss?.ForceAggro();
            Fail("儀式完成了。有東西回應了呼喚");
        }
    }

    private void Cleanup()
    {
        if (_circle != null && GodotObject.IsInstanceValid(_circle)) _circle.QueueFree();
        _circle = null; _orb = null;
    }
}

/// <summary>
/// 世界首領甦醒：a timed world event of the named layer. Every <see cref="Interval"/> seconds of play each of the four
/// world bosses wakes again; while it is awake, entering one of its two regions' overworld opens a gateway at the far
/// side of the map to its dedicated field ("rift~wb:&lt;id&gt;"). The first kill in each interval also pays the boss's
/// cache (kept in <see cref="SaveData.NamedCache"/>); later kills in the same interval pay the ordinary drop.
/// </summary>
public sealed class WorldBossEvent : WorldEvent
{
    public const double Interval = 1200.0;
    /// <summary>Test switch (--named=&lt;wb id&gt;@route): the gateway of this boss opens on the next overworld load whatever the timer says.</summary>
    public static string Force;
    public NamedDef Def;
    private string _text = "";
    public override string Text => _text;
    public override float Progress => 0f;

    private static SaveData D => Game.I?.Hero?.D;
    public static int IntervalNow => (int)Math.Floor((D?.PlayTime ?? 0.0) / Interval);
    public static double UntilNext => Interval - ((D?.PlayTime ?? 0.0) % Interval);

    /// <summary>Awake now: its cache for the current interval has not been claimed.</summary>
    public static bool Awake(NamedDef d)
    {
        var s = D; if (s == null || d == null) return false;
        s.NamedCache ??= new();
        return !s.NamedCache.TryGetValue(d.Id, out int k) || k != IntervalNow;
    }

    /// <summary>A world boss shows itself once the hero is strong enough for the stronger of its two regions and both are open.</summary>
    public static bool Eligible(NamedDef d, out string why)
    {
        why = "";
        var h = Game.I?.Hero;
        if (h == null) { why = "no hero"; return false; }
        int need = 0;
        foreach (var r in d.Regions)
        {
            var reg = ZoneDb.Region(r);
            if (reg == null) continue;
            need = Math.Max(need, reg.Level + 2);
            bool open = true;
            try { open = Quests.CanEnter(r); } catch (Exception) { }
            if (!open) { why = $"region {r} not open"; return false; }
        }
        if (h.D.Level < need) { why = $"hero level {h.D.Level} < {need}"; return false; }
        return true;
    }

    internal static void Offer(Level L)
    {
        if (L?.Region == null) return;
        foreach (var d in NamedDb.Of(NamedKind.World))
        {
            if (Array.IndexOf(d.Regions, L.Region.Id) < 0) continue;
            bool forced = Force == d.Id;
            if (!forced && !NamedAreas.LiveWorld) continue;
            if (!forced)
            {
                if (!Awake(d)) { Log.Info("named", $"world boss {d.Id}: asleep for {UntilNext:0}s more"); continue; }
                if (!Eligible(d, out string why)) { Log.Info("named", $"world boss {d.Id}: not yet ({why})"); continue; }
            }
            if (forced) Force = null;
            var spot = NamedAreas.SpotFor(L, "far", new List<Vector2>(), L.Rand ?? new Random());
            if (spot == null) { Log.Warn("named", $"world boss {d.Id}: no place for its gateway"); continue; }
            var ev = new WorldBossEvent { Def = d, L = L, Pos = spot.Value };
            L.Events.Add(ev);
            try { ev.Setup(); }
            catch (Exception e) { Log.Error("named", $"world boss gateway {d.Id} failed to set up", e); L.Events.Remove(ev); }
        }
    }

    public override void Setup()
    {
        string field = NamedAreas.FieldOf(Def.Id), from = L.AreaId;
        Obj = WorldObjects.Portal(Pos, $"世界首領・{Def.NameZh}", NamedAreas.Name(field), new Color(1f, 0.55f, 0.25f), _ => Game.I.EnterArea(field, from));
        L.AddMarker("event", $"世界首領：{Def.NameZh}", Pos, Obj);
        Game.I?.Hud?.Toast($"大地震動——世界首領「{Def.NameZh}」甦醒了", Palette.Gold);
        Snd.Ui("event_start");
        Log.Info("named", $"world boss {Def.Id}: gateway open at ({Pos.X:0},{Pos.Y:0}) in {L.AreaId}, interval {IntervalNow}");
    }

    public override void Tick(float dt)
    {
        var p = World.P;
        bool near = p != null && !p.Dead && p.Pos2.DistanceTo(Pos) < 26f;
        if (near != Active) { Active = near; L.MarkDirty(); }
        if (Active) _text = $"世界首領「{Def.NameZh}」：踏入門扉挑戰（{(Awake(Def) ? "寶藏未領" : "本輪寶藏已領")}）";
    }

    /// <summary>A world boss died: the first kill of the interval opens its cache.</summary>
    internal static void Claimed(NamedDef d)
    {
        var s = D; if (s == null || d == null) return;
        s.NamedCache ??= new();
        int k = IntervalNow;
        if (s.NamedCache.TryGetValue(d.Id, out int had) && had == k) { Log.Info("named", $"world boss {d.Id}: cache already claimed in interval {k}"); return; }
        s.NamedCache[d.Id] = k;
        var L = Level.Current; var p = World.P;
        if (L != null && p != null && L.Grid != null)
        {
            Vector2 at = L.Grid.RandomWalkNear(p.Pos2, 2f, 3.5f, L.Rand);
            L.RewardChest(at, 3, spot => { Loot.DropMaterials(15, 10, 4, spot); Loot.OpenChest(spot, 3); });
        }
        Game.I?.Hud?.Toast($"世界首領的寶藏顯現——下次甦醒約在 {UntilNext / 60.0:0} 分鐘後", Palette.Gold);
        Log.Info("named", $"world boss {d.Id}: cache claimed for interval {k}");
    }
}
