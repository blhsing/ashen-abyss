using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// --suite=gear[:diag|classes|stances|icons|loot|perf] (RENDER, slot 94): the modular hero assembly, stances, 3D item
/// icons and ground loot. Windowed with --shot=&lt;base.png&gt; it photographs every class unequipped / partly / fully
/// equipped, every stance idle and striking, the inventory with 3D icons and loot on the ground.
/// Add --gear-ph to see placeholder bodies and armour while ART-A / ART-W have not delivered.
/// </summary>
public sealed partial class TestSuite
{
    private static readonly EquipSlot[] ArmorEquipSlots = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Pants, EquipSlot.Boots };
    private static readonly ItemKind[] ArmorKinds = { ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots };

    private IEnumerator GearSuite()
    {
        string part = _arg ?? "";
        bool all = part.Length == 0;
        var parts = part.Split('+');
        bool Has(string p) => all || parts.Any(x => x == p || x.StartsWith(p + "-"));
        Log.Info("suite", $"gear: placeholders={GearCatalog.Placeholders} baked={GearCatalog.ForceBaked} off={GearCatalog.Off} manifest entries={GearCatalog.Entries.Count}");
        if (Has("diag")) yield return GearDiag();
        if (Has("classes")) yield return GearClassShots(parts.FirstOrDefault(x => x.StartsWith("classes-"))?[8..]);
        if (Has("stances")) yield return GearStanceShots();
        if (Has("icons")) yield return GearIconShots();
        if (Has("iconsheet")) yield return GearIconSheet();
        if (Has("loot")) yield return GearLootShots();
        if (Has("perf")) yield return GearPerf();
        if (Has("swap")) yield return GearSwapTest();
        if (Has("leftarm")) yield return GearLeftArm();
        if (Has("hidehelm")) yield return GearHideHelm();
        if (Has("mobileanim")) yield return GearMobileAnim();
        G.HarnessFov(0f);
        Log.Info("suite", $"gear icons: requested={ItemIcon3D.Requested} rendered={ItemIcon3D.Rendered} fromDisk={ItemIcon3D.ReadFromDisk} failed={ItemIcon3D.Failed} " +
            $"worstFrame={ItemIcon3D.WorstFrameMs:0.00}ms totalCpu={ItemIcon3D.RenderMsTotal:0}ms");
    }

    /// <summary>Puts real items on the hero (the path the game uses: Hero.D.Equip → Recalc → Player → SetGear).</summary>
    private static void Dress(Hero h, int tier, int mask, string main, string off, Rarity r = Rarity.Rare, int variant = 2)
    {
        var rng = new Random(1234);
        string key = Names.ClassKey(h.Class);
        for (int i = 0; i < 5; i++)
        {
            Item it = null;
            if (tier > 0 && (mask & (1 << i)) != 0)
            {
                it = ItemDb.Generate(300, r, h.Class, ArmorKinds[i], rng);
                it.Look = $"arm_{key}_{GearSet.ArmorSlots[i]}_t{tier}";
            }
            h.D.Equip[(int)ArmorEquipSlots[i]] = it;
        }
        Item W(string b)
        {
            if (b == null) return null;
            var it = ItemDb.Generate(300, r, h.Class, GearAdapter.IsOffHandBase(b) ? ItemKind.OffHand : ItemKind.Weapon, rng, b);
            it.Base = b;
            it.Look = $"wpn_{b}_v{variant}";
            return it;
        }
        h.D.Equip[(int)EquipSlot.MainHand] = W(main);
        h.D.Equip[(int)EquipSlot.OffHand] = W(off);
        h.Recalc();
    }

    private IEnumerator GearDiag()
    {
        _section = "gear: diag";
        // 1. grip frames: the baked greatsword (skinned to hand_r) must point where GearGrips.Hand says the weapon's +Y goes
        foreach (var (hero, mesh, bone) in new[] { ("hero_emberblade", "wpn_1", "hand_r"), ("hero_lampknight", "wpn_1", "hand_r"), ("hero_hunter", "wpn_1_off", "hand_l") })
        {
            string path = $"res://assets/chars/{hero}.glb";
            if (!ResourceLoader.Exists(path)) continue;
            var inst = GD.Load<PackedScene>(path).Instantiate<Node3D>();
            var mi = GearCatalog.Walk(inst).OfType<MeshInstance3D>().FirstOrDefault(m => m.Name == mesh);
            if (mi?.Mesh != null && mi.Skin != null)
            {
                int bind = -1;
                for (int i = 0; i < mi.Skin.GetBindCount(); i++) if (mi.Skin.GetBindName(i) == bone) bind = i;
                if (bind >= 0)
                {
                    var inv = mi.Skin.GetBindPose(bind);
                    bool right = bone == "hand_r";
                    var grip = GearGrips.Hand(right);
                    Vector3 origin = grip.Origin, far = origin; float best = -1f;
                    for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                        foreach (var v in mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                        {
                            var q = inv * v;
                            float d = q.DistanceSquaredTo(origin);
                            if (d > best) { best = d; far = q; }
                        }
                    Vector3 dir = (far - origin).Normalized();
                    Vector3 up = grip.Basis * Vector3.Back;      // the contract's +Z: up the weapon
                    float ang = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(Mathf.Abs(dir.Dot(up)), -1f, 1f)));
                    Log.Info("suite", $"gear diag: {hero}/{mesh} farthest point {Mathf.Sqrt(best):0.00} m from the grip, along {dir.X:0.00},{dir.Y:0.00},{dir.Z:0.00}; " +
                        $"grip +Y {up.X:0.00},{up.Y:0.00},{up.Z:0.00}; off by {ang:0.0}°");
                    Check(ang < 20f, $"gear diag: {hero} {mesh} lies along the grip frame", $"{ang:0.0}°");
                }
            }
            inst.Free();
        }
        // 2. every class assembles every kind of loadout without error, in the tree
        var host = new Node3D { Name = "GearDiagHost" };
        G.AddChild(host);
        foreach (ClassId c in Enum.GetValues<ClassId>())
        {
            var (lm, lo) = GearAdapter.Legacy(c);
            var m = CharModel.Create("hero_" + Names.ClassKey(c));
            host.AddChild(m);
            int errs = Log.ErrorCount;
            foreach (var g in new[] { GearSet.Make(c, 0, null, null), GearSet.Make(c, 2, lm, null, 1, Rarity.Magic, 0b01010), GearSet.Make(c, 4, lm, lo, 3, Rarity.Legendary),
                         GearSet.Make(c, 3, "sword1", "shield"), GearSet.Make(c, 3, "bow", null), GearSet.Make(c, 1, "fist", "fist"), GearSet.Starter(c) })
            {
                m.SetGear(g);
                yield return Until(() => !m.GearPending, 10, "the loadout's files");
                Check(m.Gear == g && Log.ErrorCount == errs, $"gear diag: {Names.ClassKey(c)} {g} → {m.GearReport}");
            }
            m.QueueFree();
        }
        host.QueueFree();
        yield return null;
    }

    private IEnumerator GearClassShots(string only)
    {
        foreach (ClassId cls in Enum.GetValues<ClassId>())
        {
            string key = Names.ClassKey(cls);
            if (only != null && only != key) continue;
            _section = $"gear: class {key}";
            yield return StartHero(cls, 30, "town", gear: false, god: true, slot: 94);
            var p = G.Player; var h = G.Hero;
            ClearField(false);
            Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
            p.Teleport(arena);
            p.FaceDir(new Vector2(0.35f, 1f));
            yield return 3.5f;              // the area card fades
            var (lm, lo) = GearAdapter.Legacy(cls);
            var sets = new (string name, int tier, int mask, string main, string off, Rarity r, int v)[]
            {
                ("bare", 0, 0, null, null, Rarity.Common, 1),
                ("partial", 2, 0b01010, lm, null, Rarity.Magic, 1),         // chest and pants, the class weapon
                ("full", 4, 0b11111, lm, lo, Rarity.Legendary, 3),
            };
            foreach (var s in sets)
            {
                Dress(h, s.tier, s.mask, s.main, s.off, s.r, s.v);
                p.FaceDir(new Vector2(0.35f, 1f));
                yield return 0.8f;
                yield return Snap($"{key}_{s.name}", 15f);
                Log.Info("suite", $"gear shot {key}_{s.name}: {p.Model.GearReport} idle={p.Model.Current}");
                Check(p.Model.Gear != null, $"gear: {key} {s.name} shown");
                G.Ui.Open("inventory");         // the live portrait: the same equipment, close up
                yield return 1.2f;
                yield return Snap($"{key}_{s.name}_inv", 0f);
                G.Ui.CloseAll();
                yield return 0.2f;
            }
        }
        Pass("gear: classes photographed");
    }

    private IEnumerator GearStanceShots()
    {
        var plan = new (ClassId c, string name, string main, string off)[]
        {
            (ClassId.Emberblade, "2h_melee", "sword2", null), (ClassId.Emberblade, "2h_polearm", "polearm", null),
            (ClassId.Astromancer, "2h_staff", "staff", null),
            (ClassId.Hunter, "ranged_bow", "bow", null), (ClassId.Hunter, "ranged_crossbow", "crossbow", null), (ClassId.Hunter, "dual_wield", "sword1", "dagger"),
            (ClassId.Lampknight, "1h_shield", "sword1", "shield"), (ClassId.Lampknight, "1h_lantern", "mace1", "lantern"),
            (ClassId.Pactbinder, "1h_caster", "wand", "grimoire"),
            (ClassId.Forgebody, "fists", "fist", "fist"), (ClassId.Soulbinder, "unarmed", null, null),
        };
        ClassId? current = null;
        foreach (var s in plan)
        {
            _section = $"gear: stance {s.name}";
            if (current != s.c)
            {
                yield return StartHero(s.c, 30, "town", gear: false, god: true, slot: 94);
                current = s.c;
                yield return 3f;
            }
            var p = G.Player; var h = G.Hero;
            ClearField(false);
            Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint);
            p.Teleport(arena);
            Dress(h, 3, 0b11111, s.main, s.off, Rarity.Rare, 2);
            p.FaceDir(new Vector2(0.35f, 1f));
            yield return 1.0f;
            yield return Snap($"stance_{s.name}_idle", 13f);
            G.Ui.Open("inventory");             // close-up of the same pose in the live portrait
            yield return 1.0f;
            yield return Snap($"stance_{s.name}_inv", 0f);
            G.Ui.CloseAll();
            yield return 0.2f;
            Log.Info("suite", $"gear stance {s.name}: {p.Model.Stance} idle={p.Model.Current} {p.Model.GearReport}");
            Dummy("cultist", 30, p.Pos2 + new Vector2(1.6f, 1.2f), 5e7f);
            yield return null;
            var target = World.Nearest(Faction.Player, p.Pos2, 10f);
            var basic = h.Eff(h.D.Bar[0]);
            if (target != null && basic != null)
            {
                p.TryCast(0, target.Pos2, target, true);
                yield return Mathf.Max(0.05f, basic.Contact * 0.85f);
                yield return Snap($"stance_{s.name}_attack", 13f);
                Log.Info("suite", $"gear stance {s.name}: skill {basic.Id} asked {basic.Anim} → played {p.Model.Current}");
            }
            yield return 0.8f;
            Check(p.Model.Stance.ToString().Length > 0, $"gear: stance {s.name} = {p.Model.Stance}");
        }
        Pass("gear: stances photographed");
    }

    private static List<Item> IconSamples(ClassId cls)
    {
        var rng = new Random(77);
        var list = new List<Item>();
        var bases = new[] { "sword1", "sword2", "axe1", "axe2", "mace1", "mace2", "dagger", "wand", "sceptre", "scythe1", "scythe2", "flail", "fist", "staff", "polearm", "bow", "crossbow",
                            "shield", "focus", "totem", "grimoire", "lantern" };
        var rar = new[] { Rarity.Common, Rarity.Magic, Rarity.Rare, Rarity.Legendary };
        int k = 0;
        foreach (var b in bases)
        {
            var it = ItemDb.Generate(200 + 15 * k, rar[k % 4], cls, GearAdapter.IsOffHandBase(b) ? ItemKind.OffHand : ItemKind.Weapon, rng, b);
            it.Base = b; it.Look = $"wpn_{b}_v{1 + k % 3}";
            list.Add(it); k++;
        }
        foreach (var kind in ArmorKinds.Concat(new[] { ItemKind.Ring, ItemKind.Amulet }))
            for (int t = 1; t <= 2; t++) list.Add(ItemDb.Generate(150 + 200 * t, rar[(t + k++) % 4], cls, kind, rng));
        return list;
    }

    private IEnumerator GearIconShots()
    {
        _section = "gear: icons";
        yield return StartHero(ClassId.Lampknight, 30, "town", gear: true, god: true, slot: 94);
        var h = G.Hero;
        yield return 3f;
        h.D.Bag.Clear();
        foreach (var it in IconSamples(h.Class)) if (!h.AddItem(it)) break;
        ulong t0 = Time.GetTicksMsec();
        G.Ui.Open("inventory");
        yield return Until(() => ItemIcon3D.Pending == 0 && ItemIcon3D.Requested > 0, 40, "3D icons to finish");
        double secs = (Time.GetTicksMsec() - t0) / 1000.0;
        yield return 0.3f;
        yield return Snap("inventory_icons", 0f);
        Log.Info("suite", $"gear icons: {ItemIcon3D.Requested} requested, {ItemIcon3D.Rendered} rendered, {ItemIcon3D.ReadFromDisk} from disk, {ItemIcon3D.Failed} failed in {secs:0.0}s; worst frame cost {ItemIcon3D.WorstFrameMs:0.00} ms");
        Check(ItemIcon3D.Failed == 0, "gear: every 3D icon rendered");
        // again from the disk cache
        G.Ui.CloseAll();
        yield return 0.2f;
        ItemIcon3D.Flush(false);
        int disk0 = ItemIcon3D.ReadFromDisk;
        t0 = Time.GetTicksMsec();
        G.Ui.Open("inventory");
        yield return Until(() => ItemIcon3D.Pending == 0, 20, "3D icons from disk");
        Log.Info("suite", $"gear icons from disk: {ItemIcon3D.ReadFromDisk - disk0} in {(Time.GetTicksMsec() - t0) / 1000.0:0.00}s");
        yield return Snap("inventory_icons_disk", 0f);
        G.Ui.CloseAll();
        Pass("gear: inventory with 3D icons photographed");
    }


    /// <summary>--suite=gear:iconsheet — every base at v1/v2/v3 as a 64 px icon, saved to Y:\shots\gear\icons64\ for a contact sheet.</summary>
    private IEnumerator GearIconSheet()
    {
        _section = "gear: icon sheet";
        yield return StartHero(ClassId.Emberblade, 30, "town", gear: true, god: true, slot: 94);
        yield return 1f;
        string dir = "Y:/shots/gear/icons64";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var rng = new Random(5);
        var bases = new[] { "sword1", "sword2", "axe1", "axe2", "mace1", "mace2", "dagger", "wand", "sceptre", "scythe1", "scythe2", "flail", "fist", "staff", "polearm", "bow", "crossbow",
                            "shield", "focus", "totem", "grimoire", "lantern" };
        var rar = new[] { Rarity.Magic, Rarity.Rare, Rarity.Legendary };
        var list = new List<(string file, AtlasTexture tex)>();
        var index = new System.Text.StringBuilder();
        foreach (var b in bases)
        {
            index.Append(b).Append('|').Append(ItemDb.BaseOf(b)?.Name ?? b).Append('\n');
            for (int v = 1; v <= 3; v++)
            {
                var it = ItemDb.Generate(300, rar[v - 1], G.Hero.Class, GearAdapter.IsOffHandBase(b) ? ItemKind.OffHand : ItemKind.Weapon, rng, b);
                it.Base = b; it.Look = $"wpn_{b}_v{v}";
                if (ItemIcon3D.Get(it, 64) is AtlasTexture t) list.Add(($"{dir}/{b}_v{v}.png", t));
                else Log.Warn("suite", $"icon sheet: no 3D icon for {b} v{v}");
            }
        }
        yield return Until(() => ItemIcon3D.Pending == 0, 60, "the 64 px icons");
        yield return 0.2f;
        int n = 0;
        foreach (var (file, t) in list)
        {
            var img = t.Atlas?.GetImage();
            if (img == null || img.IsEmpty()) continue;
            if (img.IsCompressed()) img.Decompress();
            img.ClearMipmaps();
            img.SavePng(file); n++;
        }
        using (var f = FileAccess.Open(dir + "/index.txt", FileAccess.ModeFlags.Write)) f?.StoreString(index.ToString());
        Check(n == bases.Length * 3, $"gear: icon sheet {n}/{bases.Length * 3} icons saved to {dir}");
    }


    /// <summary>
    /// --suite=gear:swap — a full 2H ↔ dual-wield swap of every slot in combat, as a gear set does it: both sets preloaded
    /// with GearCatalog.PreloadSets, all slots written, one Hero.Changed per slot. Checks one rebuild per swap and measures
    /// the frames around it.
    /// </summary>
    private IEnumerator GearSwapTest()
    {
        _section = "gear: set swap";
        yield return StartHero(ClassId.Emberblade, 30, "wastes", gear: false, god: true, slot: 94);
        var p = G.Player; var h = G.Hero;
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 8);
        p.Teleport(arena);
        yield return 2f;
        var rng = new Random(9);
        Item W(string b, Rarity r) { var it = ItemDb.Generate(300, r, h.Class, GearAdapter.IsOffHandBase(b) ? ItemKind.OffHand : ItemKind.Weapon, rng, b); it.Base = b; it.Look = $"wpn_{b}_v{(r >= Rarity.Legendary ? 3 : 2)}"; return it; }
        Item A(ItemKind k, int t) { var it = ItemDb.Generate(300, Rarity.Rare, h.Class, k, rng); it.Look = $"arm_{Names.ClassKey(h.Class)}_{k.ToString().ToLowerInvariant()}_t{t}"; return it; }
        // three real sets (saved with Hero.SaveSet): 2H, dual-wield, 1H + off-hand, each with its own armour
        var plans = new (string name, Item main, Item off, int tier)[]
        {
            ("2H", W("sword2", Rarity.Legendary), null, 4), ("dual", W("sword1", Rarity.Rare), W("axe1", Rarity.Rare), 2), ("1H+off", W("mace1", Rarity.Rare), W("shield", Rarity.Rare), 3),
        };
        h.D.Bag.Clear();
        for (int s = 0; s < plans.Length; s++)
        {
            h.D.Equip[(int)EquipSlot.MainHand] = plans[s].main;
            h.D.Equip[(int)EquipSlot.OffHand] = plans[s].off;
            for (int i = 0; i < 5; i++) h.D.Equip[(int)ArmorEquipSlots[i]] = A(ArmorKinds[i], plans[s].tier);
            h.Recalc();
            h.SaveSet(s);                                           // pins the set's models (PreloadSetModels)
            foreach (var it in h.D.Equip) if (it != null && !h.D.Bag.Contains(it)) h.D.Bag.Add(it);
        }
        GearSets.Switch(0, true);
        yield return 3f;                // the loader thread
        for (int i = 0; i < 4; i++) Dummy("cultist", 30, p.Pos2 + new Vector2(Mathf.Cos(i * 1.6f), Mathf.Sin(i * 1.6f)) * 2f, 5e7f);
        yield return 0.3f;
        // F2, E, F3, E, F1, F2, E, F3 … in combat
        var keys = new[] { 1, -1, 2, -1, 0, 1, -1, 2, -1, 0, 2, 1 };
        double worst = 0, sum = 0; int frames = 0, rebuilds = 0, pendingSeen = 0, swaps = 0, changed = 0;
        var perSwap = new List<string>();
        foreach (var k in keys)
        {
            var target = World.Nearest(Faction.Player, p.Pos2, 10f);
            if (target != null) p.TryCast(0, target.Pos2, target, true);
            yield return null;
            string sig0 = p.Model.GearSignature;
            int r0 = CharModel.GearRebuilds;
            ulong t0 = Time.GetTicksUsec();
            bool ok = k < 0 ? GearSets.QuickSwap() : GearSets.Switch(k, true);
            swaps++;
            ulong last = t0; double w1 = 0;
            for (int f = 0; f < 6; f++)
            {
                yield return null;
                ulong now = Time.GetTicksUsec();
                double ms = (now - last) / 1000.0; last = now;
                w1 = Math.Max(w1, ms); sum += ms; frames++;
                if (p.Model.GearPending) pendingSeen++;
            }
            worst = Math.Max(worst, w1);
            rebuilds += CharModel.GearRebuilds - r0;
            if (p.Model.GearSignature != sig0) changed++;
            perSwap.Add($"{(k < 0 ? "E" : "F" + (k + 1))}→{p.Model.Stance}:{w1:0}ms{(ok ? "" : "(refused)")}");
            yield return (float)GearSets.CooldownSec + 0.1f;         // the shared set cooldown
        }
        Log.Info("suite", $"gear set swap (real sets): {swaps} swaps in combat, {rebuilds} rebuilds, frames waiting for files {pendingSeen}; " +
            $"pieces pre-built hidden {CharModel.PrebuiltPieces}; " +
            $"worst frame after a swap {worst:0.0} ms, mean {sum / Math.Max(1, frames):0.0} ms; {string.Join(" ", perSwap)}");
        Check(rebuilds == changed, "gear: one rebuild per set swap that changes the loadout", $"{rebuilds} rebuilds for {changed} changes in {swaps} swaps");
        Check(pendingSeen == 0, "gear: preloaded sets never wait for files");
    }

    /// <summary>
    /// --suite=gear:leftarm — the user report "no left arm" (lampknight, polearm): every two-hand base on the lampknight with
    /// helm t2 / chest t3 / gloves t3 / pants t2 / boots t3, idle, running and striking, from the game camera and from the
    /// side, with the grip IK as first made (mode 1, "before") and with the safe grip point (mode 2, "after"). Logs where the
    /// left wrist and elbow are against grip2 and the torso.
    /// </summary>
    private IEnumerator GearLeftArm()
    {
        _section = "gear: left arm";
        yield return StartHero(ClassId.Lampknight, 31, "frost", gear: false, god: true, slot: 94);
        var p = G.Player; var h = G.Hero;
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 8);
        p.Teleport(arena);
        yield return 2.5f;
        var rng = new Random(3);
        string key = Names.ClassKey(h.Class);
        int[] tiers = { 2, 3, 3, 2, 3 };
        for (int i = 0; i < 5; i++)
        {
            var it = ItemDb.Generate(300, Rarity.Rare, h.Class, ArmorKinds[i], rng);
            it.Look = $"arm_{key}_{GearSet.ArmorSlots[i]}_t{tiers[i]}";
            h.D.Equip[(int)ArmorEquipSlots[i]] = it;
        }
        string dir = "Y:/shots/gear/leftarm";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var sk = p.Model.Skeleton;
        int hl = sk.FindBone("hand_l"), ll = sk.FindBone("lowerarm_l"), pel = sk.FindBone("pelvis"), neck = sk.FindBone("neck_01");
        string Measure(string what)
        {
            var xf = sk.GlobalTransform;
            Vector3 wrist = xf * sk.GetBoneGlobalPose(hl).Origin, elbow = xf * sk.GetBoneGlobalPose(ll).Origin;
            Vector3 a = sk.GetBoneGlobalPose(pel).Origin, b = sk.GetBoneGlobalPose(neck).Origin, lw = sk.GetBoneGlobalPose(hl).Origin, le = sk.GetBoneGlobalPose(ll).Origin;
            float Dist(Vector3 q) { var ab = b - a; float t = Mathf.Clamp((q - a).Dot(ab) / ab.LengthSquared(), 0, 1); return q.DistanceTo(a + ab * t); }
            var ik = p.Model.GripIK;
            string g = ik == null ? "no IK" : $"grip2 {xf * ik.LastGrip2:F2} inside={ik.LastGrip2Inside} outOfReach={ik.LastGrip2OutOfReach} target {xf * ik.LastTarget:F2} t={ik.LastT:0.00} weight={ik.LastWeight:0.00} wrist→grip2 {(xf * ik.LastGrip2).DistanceTo(wrist):0.00}m";
            return $"{what}: wrist {wrist:F2} (from torso axis {Dist(lw):0.00} m) elbow {elbow:F2} ({Dist(le):0.00} m); {g}; hidden [{p.Model.HiddenBody}]";
        }
        var bases = new[] { "polearm", "sword2", "axe2", "mace2", "staff", "scythe2", "bow" };
        foreach (var b in bases)
        {
            var wpn = ItemDb.Generate(300, Rarity.Rare, h.Class, ItemKind.Weapon, rng, b);
            wpn.Base = b; wpn.Look = $"wpn_{b}_v2";
            h.D.Equip[(int)EquipSlot.MainHand] = wpn; h.D.Equip[(int)EquipSlot.OffHand] = null;
            h.Recalc();
            yield return Until(() => !p.Model.GearPending && p.Model.Gear != null && p.Model.Gear.Main?.Base == b, 10, b);
            foreach (int mode in b == "polearm" ? new[] { 1, 2 } : new[] { 2 })
            {
                GearGripIK.Mode = mode;
                string tag = $"{b}_{(mode == 1 ? "before" : "after")}";
                ClearField(false);
                p.Teleport(arena); p.SetMove(arena, false);
                yield return 0.6f;
                foreach (var (view, face) in new[] { ("cam", new Vector2(0.35f, 1f)), ("side", new Vector2(1f, 0f)) })
                {
                    p.FaceDir(face);
                    yield return 0.5f;
                    Log.Info("suite", "leftarm " + Measure($"{tag} idle {view} [{p.Model.Current}]"));
                    if (b == "polearm" || view == "cam") yield return Snap($"{tag}_idle_{view}", 13f);
                }
                // run
                p.SetMove(arena + new Vector2(6f, 0f), false);
                yield return 0.45f;
                Log.Info("suite", "leftarm " + Measure($"{tag} run [{p.Model.Current}]"));
                if (b == "polearm") yield return Snap($"{tag}_run", 13f);
                yield return Until(() => p.Pos2.DistanceTo(arena + new Vector2(6f, 0f)) < 0.8f, 4, "arrive");
                // strike
                Dummy("cultist", 30, p.Pos2 + new Vector2(1.6f, 0.8f), 5e7f);
                yield return null;
                var target = World.Nearest(Faction.Player, p.Pos2, 10f);
                var basic = h.Eff(h.D.Bar[0]);
                if (target != null && basic != null)
                {
                    p.TryCast(0, target.Pos2, target, true);
                    yield return Mathf.Max(0.05f, basic.Contact * 0.85f);
                    Log.Info("suite", "leftarm " + Measure($"{tag} attack [{p.Model.Current}]"));
                    yield return Snap($"{tag}_attack", 13f);
                }
                yield return 0.6f;
            }
        }
        GearGripIK.Mode = 2;
        Pass("gear: left arm measured");
    }

    /// <summary>--suite=gear:hidehelm — the "hide headwear" option: full t4 on several classes, helm shown then hidden (world and inventory portrait).</summary>
    private IEnumerator GearHideHelm()
    {
        foreach (var cls in new[] { ClassId.Lampknight, ClassId.Hunter, ClassId.Emberblade, ClassId.Astromancer })
        {
            _section = $"gear: hide helm {cls}";
            yield return StartHero(cls, 30, "town", gear: false, god: true, slot: 94);
            var p = G.Player; var h = G.Hero;
            ClearField(false);
            p.Teleport(FindArena(World.Grid, G.Level.Checkpoint));
            var (lm, lo) = GearAdapter.Legacy(cls);
            Dress(h, 4, 0b11111, lm, lo, Rarity.Legendary, 3);
            yield return 2.5f;
            string key = Names.ClassKey(cls);
            foreach (bool hide in new[] { false, true })
            {
                GearAdapter.SetHideHelm(h, hide);
                p.FaceDir(new Vector2(0.35f, 1f));
                yield return Until(() => !p.Model.GearPending, 5, "gear");
                yield return 0.5f;
                bool helmShown = p.Model.GearReport.Contains("_helm_");
                Check(helmShown != hide, $"gear: {key} helm {(hide ? "hidden" : "shown")}", p.Model.GearReport);
                yield return Snap($"{key}_{(hide ? "nohelm" : "helm")}_world", 9f);
                G.Ui.Open("inventory");
                yield return 1.2f;
                yield return Snap($"{key}_{(hide ? "nohelm" : "helm")}_inv", 0f);
                G.Ui.CloseAll();
                yield return 0.2f;
            }
            Check(h.D.HideHelm && h.D.Equip[(int)EquipSlot.Helm] != null, $"gear: {key} helm still worn while hidden");
            GearAdapter.SetHideHelm(h, false);
        }
    }

    /// <summary>
    /// --suite=gear:mobileanim — the upper-body layer: every ranged loadout shooting while it walks toward the aim, across
    /// it and away from it (back-pedal). The player's own update is paused; the test drives CharModel.PlayUpperAttack /
    /// SetLocomotion / EndUpper as the moving-cast code will.
    /// </summary>
    private IEnumerator GearMobileAnim()
    {
        var plan = new (ClassId c, string name, string main, string off)[]
        {
            (ClassId.Hunter, "hunter_crossbow", "crossbow", null), (ClassId.Hunter, "hunter_bow", "bow", null),
            (ClassId.Astromancer, "astromancer_staff", "staff", null), (ClassId.Pactbinder, "pactbinder_wand", "wand", "grimoire"),
            (ClassId.Soulbinder, "soulbinder_scythe", "scythe2", null),
        };
        ClassId? current = null;
        foreach (var s in plan)
        {
            _section = $"gear: mobile anim {s.name}";
            if (current != s.c) { yield return StartHero(s.c, 30, "wastes", gear: false, god: true, slot: 94); current = s.c; yield return 2f; }
            var p = G.Player; var h = G.Hero; var m = p.Model;
            ClearField();
            Dress(h, 4, 0b11111, s.main, s.off, Rarity.Legendary, 3);
            yield return Until(() => !m.GearPending, 5, "gear");
            Vector2 home = FindArena(World.Grid, G.Level.Checkpoint, 8);
            var basic = h.Eff(h.D.Bar[0]);
            string clip = Stances.Remap(m.Stance, basic?.Anim ?? "Spell_Simple_Shoot", m.Gear);
            Vector2 aim2 = new Vector2(0.45f, 1f).Normalized();          // toward the lens, a little to the right
            var dirs = new (string tag, Vector2 move)[] { ("forward", aim2), ("sideways", new Vector2(aim2.Y, -aim2.X)), ("backwards", -aim2) };
            foreach (var (tag, move) in dirs)
            {
                p.Teleport(home - move * 3f);
                p.FaceDir(aim2);
                p.SetProcess(false); p.SetPhysicsProcess(false);
                yield return null;
                Vector3 aim3 = new(aim2.X, 0, aim2.Y), move3 = new(move.X, 0, move.Y);
                float t = 0f, nextShot = 0f; bool shot = false;
                ulong last = Time.GetTicksMsec();
                while (t < 2.6f)
                {
                    float dt = (Time.GetTicksMsec() - last) / 1000f; last = Time.GetTicksMsec();
                    t += dt;
                    if (t >= nextShot) { m.PlayUpperAttack(clip, 1f, 0.3f, aim3); nextShot = t + 0.95f; }
                    p.Teleport(p.Pos2 + move * 2.6f * dt);
                    p.FaceDir(aim2);
                    m.SetLocomotion(move3, 2.6f, aim3);
                    if (!shot && t > 1.6f + 0.28f) { shot = true; yield return Snap($"{s.name}_{tag}", 11f); last = Time.GetTicksMsec(); continue; }
                    yield return null;
                }
                Log.Info("suite", $"mobile anim {s.name} {tag}: upper clip {clip}, legs {m.Current} rate {m.Anim?.SpeedScale:0.00}, layered={m.Layered} upperBusy={m.UpperBusy}");
                m.EndUpper();
                p.SetProcess(true); p.SetPhysicsProcess(true);
                yield return 0.4f;
            }
        }
        Pass("gear: moving-cast poses photographed");
    }

    private IEnumerator GearLootShots()
    {
        _section = "gear: loot";
        yield return StartHero(ClassId.Emberblade, 30, "wastes", gear: true, god: true, slot: 94);
        var p = G.Player;
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 8);
        p.Teleport(arena);
        p.FaceDir(Vector2.Down);
        yield return 3f;
        var items = IconSamples(G.Hero.Class);
        var pick = new[] { 1, 3, 15, 16, 17, 22, 24, 30, 32 }.Where(i => i < items.Count).Select(i => items[i]).ToList();
        for (int i = 0; i < pick.Count; i++)
        {
            float a = i * Mathf.Tau / pick.Count;
            Loot.DropItem(pick[i], arena + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.2f);
        }
        yield return 2.5f;
        p.Teleport(arena + new Vector2(0f, -2.5f));
        yield return 0.5f;
        yield return Snap("ground_loot", 24f);
        int models = G.GetTree().Root.FindChildren("LootModel", "", true, false).Count;
        Log.Info("suite", $"gear loot: {pick.Count} items dropped, {models} shown as their own 3D model");
        Pass("gear: ground loot photographed");
    }

    private IEnumerator GearPerf()
    {
        _section = "gear: perf";
        yield return StartHero(ClassId.Lampknight, 30, "town", gear: true, god: true, slot: 94);
        var p = G.Player; var h = G.Hero;
        yield return 2f;
        double vram0 = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576.0;
        var (lm, lo) = GearAdapter.Legacy(h.Class);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int n = 0;
        double worst = 0;
        for (int i = 0; i < 24; i++)
        {
            long t = sw.ElapsedTicks;
            Dress(h, 1 + i % 4, i % 2 == 0 ? 0b11111 : 0b00110, i % 3 == 0 ? lm : "mace1", i % 3 == 0 ? lo : "shield", (Rarity)(i % 4), 1 + i % 3);
            worst = Math.Max(worst, (sw.ElapsedTicks - t) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            n++;
            yield return null;
        }
        double vram1 = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576.0;
        Log.Info("suite", $"gear perf: {n} re-dresses, worst {worst:0.0} ms (item generation included); vram {vram0:0} → {vram1:0} MB; " +
            $"meshes on the hero {p.Model.GetChildCount()} report {p.Model.GearReport}");
        Pass("gear: perf measured");
    }
}
