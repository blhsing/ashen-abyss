using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenAbyss;

/// <summary>
/// The forged iron golem (tools/golems/PLAN.md §2a): which items can be forged, what the golem inherits from one at a
/// minion scale, the preview, the forging itself (the item is consumed; a full copy is kept in SaveData.GolemForge, so
/// re-forming never costs it again) and the look (the item's own model fused onto the golem's arm, torso or head bones).
/// </summary>
public static class GolemForge
{
    // ── material ──
    // Weapons: blade and head bases are metal; a wooden club (the first tier of 巨錘) is not. Shields: metal from the second
    // tier (鐵邊盾) up. Focus, totem, grimoire, lantern, wand, staff, bow, crossbow: no. Armour: the plate classes' pieces
    // (燼刃衛, 誓燈騎士) always; any class's heavy tiers (t3, t4) carry enough plate. Rings, amulets and gems never.
    // A forged copy keeps working if these rules change (Inherit never re-checks the material).
    private static readonly HashSet<string> MetalBases = new() { "sword1", "axe1", "mace1", "dagger", "scythe1", "flail", "fist", "sceptre", "sword2", "axe2", "mace2", "polearm", "scythe2", "shield" };
    private static readonly HashSet<string> PlateClasses = new() { "emberblade", "lampknight" };
    private static int Tier(int power) => Math.Clamp(power / 125, 0, 4);

    public static bool IsMetal(Item it)
    {
        if (it == null) return false;
        switch (it.Kind)
        {
            case ItemKind.Weapon: case ItemKind.OffHand:
                if (it.Base == null || !MetalBases.Contains(it.Base)) return false;
                if ((it.Base == "mace2" || it.Base == "shield") && Tier(it.Power) == 0 && it.Rarity < Rarity.Legendary) return false;      // 枯木戰棍, 木製圓盾
                return true;
            case ItemKind.Helm: case ItemKind.Chest: case ItemKind.Gloves: case ItemKind.Pants: case ItemKind.Boots:
            {
                string look = it.Look ?? "";
                int tier = 0;
                int u = look.LastIndexOf("_t", StringComparison.Ordinal);
                if (u > 0) int.TryParse(look[(u + 2)..], out tier);
                string cls = look.StartsWith("arm_") && look.IndexOf('_', 4) > 4 ? look[4..look.IndexOf('_', 4)] : "";
                return PlateClasses.Contains(cls) || tier >= 3;
            }
            default: return false;
        }
    }

    /// <summary>Why an item can not be forged (null when it can). Locked, worn, gear-set, non-metal and jewellery are refused.</summary>
    public static string Refusal(Hero h, Item it)
    {
        if (it == null) return "沒有物品。";
        if (it.Kind is ItemKind.Ring or ItemKind.Amulet or ItemKind.Gem) return "戒指、項鍊與寶石無法鑄造。";
        if (Array.IndexOf(h.D.Equip, it) >= 0) return "穿戴中的裝備無法鑄造，請先卸下。";
        if (it.Locked) return "已鎖定的物品需先解除鎖定。";
        if (h.InSet(it)) return "此物品屬於某個裝備組，請先將它移出裝備組。";
        if (!IsMetal(it)) return "只有金屬的武器、盾牌與護甲能鑄成魔像。";
        if (!h.D.Bag.Contains(it)) return "物品必須在行囊中。";
        return null;
    }

    /// <summary>Bag items that can be forged. <paramref name="salvageOnly"/>: only those marked as junk (the AI pilot's rule).</summary>
    public static List<Item> Candidates(Hero h, bool salvageOnly = false) =>
        h.D.Bag.Where(it => Refusal(h, it) == null && (!salvageOnly || (h.D.Junk?.Contains(it.Uid) ?? false))).ToList();

    public static GolemStyle StyleOf(Item it)
    {
        if (it == null) return GolemStyle.Fists;
        if (it.Kind == ItemKind.OffHand) return it.Base == "shield" ? GolemStyle.Shield : GolemStyle.Fists;
        if (it.Kind != ItemKind.Weapon) return GolemStyle.Fists;
        if (it.Base is "polearm" or "scythe2") return GolemStyle.Polearm;
        return (ItemDb.BaseOf(it.Base)?.Hands ?? 1) == 2 ? GolemStyle.TwoHand : GolemStyle.OneHand;
    }

    public static string StyleName(GolemStyle s) => s switch
    {
        GolemStyle.TwoHand => "雙手重擊：緩慢的過頂猛砸", GolemStyle.OneHand => "單手連擊：較快的雙擊", GolemStyle.Polearm => "長柄橫掃：大範圍掃擊",
        GolemStyle.Shield => "持盾：格擋並以盾擊取代第 4 擊", _ => "熔渣拳頭",
    };

    // legendary powers and unique effects a golem can sensibly use (on-hit, thorns, auras, procs)
    private static readonly HashSet<string> UsablePowers = new()
    {
        "ember_burst", "storm_echo", "executioner", "vuln_mark", "soul_harvest", "chill_touch", "thornmail", "iron_will", "second_wind", "legion",
        "sb_bone_shards", "sb_iron_fissure", "sb_blood_harvest", "sb_undying",
        "u_reaper", "u_snuffer", "u_cairn", "u_crucible",
    };

    /// <summary>What the golem inherits from an item, at a minion scale (the hero's level caps the golem's).</summary>
    public static GolemStats Inherit(Item it, Hero h)
    {
        var g = new GolemStats { Style = StyleOf(it) };
        if (it == null) return g;
        int heroLevel = h?.D.Level ?? 1;
        g.Level = Math.Clamp(it.Power / 10 - 2, 1, heroLevel);
        float bestEl = 0f;
        void Add(Affix a)
        {
            float v = it.Eff(a);
            switch (a.Stat)
            {
                case Stat.DmgAll: case Stat.DmgDot: case Stat.DmgElite: case Stat.DmgCC: case Stat.DmgVuln: case Stat.DmgOverpower:
                case Stat.DmgBasic: case Stat.DmgCore: case Stat.DmgUlt: case Stat.CritDmg:
                    g.DmgPct += v * 0.5f; break;
                case Stat.DmgPhysical: case Stat.DmgFire: case Stat.DmgFrost: case Stat.DmgLightning: case Stat.DmgPoison: case Stat.DmgShadow:
                    g.DmgPct += v * 0.5f;
                    if (a.Stat != Stat.DmgPhysical && v > bestEl) { bestEl = v; g.Element = a.Stat switch { Stat.DmgFire => Element.Fire, Stat.DmgFrost => Element.Frost, Stat.DmgLightning => Element.Lightning, Stat.DmgPoison => Element.Poison, _ => Element.Shadow }; }
                    break;
                case Stat.MinionDmg: case Stat.GolemDmg: g.DmgPct += v; break;
                case Stat.AttackSpeed: case Stat.MinionAtkSpeed: g.AtkSpdPct += v; break;
                case Stat.CritChance: g.CritPct += v; break;
                case Stat.MaxLife: g.LifePct += v / Balance.BaseLife(heroLevel) * 50f; break;
                case Stat.LifePct: case Stat.GolemLife: g.LifePct += v; break;
                case Stat.Armor: g.ArmorFlat += v; break;
                case Stat.ResAll: g.ResPct += v; break;
                case Stat.ResFire: case Stat.ResFrost: case Stat.ResLightning: case Stat.ResPoison: case Stat.ResShadow: g.ResPct += v * 0.25f; break;
                case Stat.DmgReduction: g.DrPct += v; break;
                case Stat.Block: g.BlockPct += v; break;
                case Stat.Thorns: g.Thorns += v; break;
                case Stat.LifeOnHit: g.LifeOnHit += v; break;
                case Stat.LifeOnKill: case Stat.LifeRegen: g.LifeOnHit += v * 0.25f; break;
                default: g.GeneralPct += 2f; break;        // hero-only (attributes, resource, cooldowns, movement, skill ranks …): a small general bonus
            }
        }
        foreach (var a in it.Affixes) Add(a);
        if (it.Implicits != null) foreach (var a in it.Implicits) Add(a);
        if (it.Kind == ItemKind.Weapon)
        {
            float refDps = ItemDb.WeaponAvg(Math.Clamp((heroLevel + 2) * 10, 10, ItemDb.MaxPower)) * (it.TwoHand ? 1f : ItemDb.OneHandDps);
            float dps = it.AvgDmg * it.Aps;
            g.WeaponPct = 60f * Mathf.Clamp(dps / Mathf.Max(1f, refDps * (ItemDb.BaseOf(it.Base)?.Aps ?? 1f)), 0.2f, 2.5f);
        }
        g.ArmorFlat += it.Armor * it.BaseMul;
        if (it.Kind == ItemKind.OffHand && it.Base == "shield") g.BlockPct += 10f;

        string power = it.Unique ?? it.Aspect;
        if (power != null)
        {
            if (it.Unique != null) g.PowerName = ItemDb.UniqueOf(it.Unique)?.Name ?? it.Unique;
            else { var asp = ItemDb.Aspect(it.Aspect); g.PowerName = asp?.Name ?? it.Aspect; g.PowerValue = asp?.Value(it.AspectRoll) ?? 0f; }
            if (UsablePowers.Contains(power))
            {
                g.Power = power == "second_wind" ? "sb_undying" : power;
                if (power == "thornmail") g.Thorns *= 1f + g.PowerValue / 100f;
            }
            else { g.Power = power; g.PowerUnusable = true; }
        }
        // the summary the tooltips and the minion frame list
        void L(float v, string what) { if (v > 0.05f) g.Lines.Add($"{what} +{v:0.#}%"); }
        L(g.WeaponPct, "魔像攻擊（武器）"); L(g.DmgPct, "魔像傷害"); L(g.AtkSpdPct, "魔像攻擊速度"); L(g.CritPct, "魔像爆擊機率"); L(g.LifePct, "魔像生命");
        if (g.ArmorFlat > 0.5f) g.Lines.Add($"魔像護甲 +{g.ArmorFlat:0}");
        L(g.ResPct, "魔像抗性"); L(g.DrPct, "魔像傷害減免"); L(g.BlockPct, "魔像格擋機率");
        if (g.Thorns > 0.5f) g.Lines.Add($"魔像荊棘 +{g.Thorns:0}");
        if (g.LifeOnHit > 0.5f) g.Lines.Add($"魔像擊中恢復生命 +{g.LifeOnHit:0}");
        L(g.GeneralPct, "魔像全面強化");
        if (g.Element != Element.Physical) g.Lines.Add($"攻擊帶有{Names.Element(g.Element)}之力");
        g.Lines.Add($"攻擊方式：{StyleName(g.Style)}");
        if (g.PowerName != null) g.Lines.Add(g.PowerUnusable ? $"「{g.PowerName}」：魔像無法使用" : $"「{g.PowerName}」：魔像可使用");
        return g;
    }

    /// <summary>One number to compare forges (damage × toughness of the golem); the AI pilot forges only when it rises.</summary>
    public static float Score(Item it, Hero h)
    {
        if (it == null) return 1f;
        var g = Inherit(it, h);
        float off = g.TotalDmgMul * (1f + g.AtkSpdPct / 100f) * (1f + g.CritPct / 200f);
        float def = (1f + g.LifePct / 100f) * (1f + Mathf.Min(60f, g.ResPct + g.DrPct + g.BlockPct * 0.5f) / 100f) * (1f + g.ArmorFlat / (g.ArmorFlat + 600f));
        return off * Mathf.Sqrt(def) * (g.PowerUnusable || g.PowerName == null ? 1f : 1.1f);
    }

    /// <summary>The preview for the confirmation: the golem's resulting stats against the current ones (GearEval-style percentages).</summary>
    public static List<string> Preview(Hero h, Item it)
    {
        var l = new List<string>();
        var cur = h.D.GolemForge;
        float now = Score(cur, h), next = Score(it, h);
        float diff = (next / Mathf.Max(0.01f, now) - 1f) * 100f;
        l.Add(diff >= 0f ? $"鑄造後：魔像整體強度 +{diff:0}%" : $"鑄造後：魔像整體強度 {diff:0}%");
        var a = Inherit(cur, h); var b = Inherit(it, h);
        void Cmp(string what, float x, float y, string unit = "%") { if (Mathf.Abs(x - y) > 0.05f) l.Add($"{what}：{x:0.#}{unit} → {y:0.#}{unit}"); }
        Cmp("魔像攻擊", (a.TotalDmgMul - 1f) * 100f, (b.TotalDmgMul - 1f) * 100f);
        Cmp("魔像攻擊速度", a.AtkSpdPct, b.AtkSpdPct);
        Cmp("魔像生命", a.LifePct, b.LifePct);
        Cmp("魔像護甲", a.ArmorFlat, b.ArmorFlat, "");
        Cmp("魔像抗性", a.ResPct, b.ResPct);
        Cmp("魔像格擋機率", a.BlockPct, b.BlockPct);
        l.Add($"攻擊方式：{StyleName(b.Style)}");
        if (b.PowerName != null) l.Add(b.PowerUnusable ? $"「{b.PowerName}」：魔像無法使用" : $"「{b.PowerName}」：魔像可使用");
        if (cur != null) l.Add($"目前的精華「{cur.Name}」將會消失。");
        return l;
    }

    /// <summary>The inventory's verdict line on a metal item for a Soulbinder (「鑄造後：魔像攻擊 +x%」); null when it does not apply.</summary>
    public static string Verdict(Hero h, Item it)
    {
        if (h == null || h.Class != ClassId.Soulbinder || !IsMetal(it)) return null;
        var (atk, arm) = Deltas(h, it);
        static string S(float v) => v >= 0f ? $"+{v:0}%" : $"{v:0}%";
        return $"鑄造後：魔像攻擊 {S(atk)}　護甲 {S(arm)}";
    }

    /// <summary>The golem's attack and armour change (per cent) if forged from this item instead of its current essence.</summary>
    public static (float atk, float armor) Deltas(Hero h, Item it)
    {
        var a = Inherit(h.D.GolemForge, h); var b = Inherit(it, h);
        float atkA = a.TotalDmgMul * (1f + a.AtkSpdPct / 100f), atkB = b.TotalDmgMul * (1f + b.AtkSpdPct / 100f);
        float baseArmor = h.Armor * 0.6f;
        float armA = baseArmor + a.ArmorFlat, armB = baseArmor + b.ArmorFlat;
        return ((atkB / Mathf.Max(0.01f, atkA) - 1f) * 100f, (armB / Mathf.Max(1f, armA) - 1f) * 100f);
    }

    /// <summary>The item tooltip's forge line for a Soulbinder (BBCode), with why it cannot be forged now; null when it does not apply.</summary>
    public static string TooltipLine(Hero h, Item it)
    {
        string v = Verdict(h, it);
        if (v == null) return null;
        var (atk, _) = Deltas(h, it);
        string col = atk >= 0f ? "b89b6a" : "9a7f78";
        string why = Array.IndexOf(h.D.Equip, it) >= 0 ? "（穿戴中）" : it.Locked ? "（已鎖定）" : h.InSet(it) ? "（在裝備組中）" : "";
        return $"[color=#{col}]{v}{why}[/color]";
    }

    private static readonly JsonSerializerOptions Opt = new() { IncludeFields = true };
    public static Item Copy(Item it) => JsonSerializer.Deserialize<Item>(JsonSerializer.Serialize(it, Opt), Opt);

    /// <summary>Consumes a bag item into the iron golem (the essence replaces the old one). False with the reason when refused.</summary>
    public static bool Forge(Hero h, Item it, out string why)
    {
        why = Refusal(h, it);
        if (why != null) { Log.Info("golem", $"forge refused: {it?.Name}: {why}"); return false; }
        if (h.BookKindId(ClassBook.Golem) != "g_iron") { why = "需要在亡者名冊中選擇鐵魔像。"; return false; }
        string old = h.D.GolemForge?.Name;
        h.D.Bag.Remove(it);
        h.D.Junk?.Remove(it.Uid);
        h.D.GolemForge = Copy(it);
        h.Recalc();
        Log.Info("golem", $"forged the iron golem from {it.Name} ({it.Rarity}, {it.Base}, power {it.Power}); essence {(old == null ? "new" : $"replaces {old}")}; {string.Join("; ", Inherit(it, h).Lines)}");
        Snd.Ui("anvil");
        return true;
    }

    // ── attack style ──

    private static readonly Dictionary<string, MonsterDef> Styled = new();

    /// <summary>The iron golem's definition with the attack clips and timing of its weapon class.</summary>
    public static MonsterDef StyledDef(MonsterDef def, GolemStyle s)
    {
        if (s == GolemStyle.Fists) return def;
        string key = def.Id + "#" + s;
        if (Styled.TryGetValue(key, out var d)) return d;
        d = def.Variant(def.Id, def.Name, def.Tint, m =>
        {
            switch (s)
            {
                case GolemStyle.TwoHand: m.AttackAnims = new[] { "Sword_Heavy_Combo", "Sword_Attack" }; m.Cooldown = 2.5f; m.AnimSpeed = 0.85f; m.Contact = 0.7f; break;
                case GolemStyle.OneHand: m.AttackAnims = new[] { "Sword_Regular_A", "Sword_Regular_B" }; m.Cooldown = 1.3f; m.AnimSpeed = 1.15f; m.Contact = 0.36f; break;
                case GolemStyle.Polearm: m.AttackAnims = new[] { "Sword_Regular_C", "Sword_Attack" }; m.Cooldown = 2.0f; m.Range = 3.4f; break;
                case GolemStyle.Shield: m.AttackAnims = new[] { "Shield_OneShot", "Sword_Regular_A" }; m.Cooldown = 1.6f; break;
            }
        });
        Styled[key] = d;
        return d;
    }

    // ── look: the item's own model fused onto the stand-in's nearest bones ──

    /// <summary>Fuses the forged item's model onto the golem (weapons on the arms, armour as plates on torso / head / limbs).</summary>
    public static void Fuse(Monster m, Item it, MinionArt art)
    {
        var model = m.Model;
        var sk = model?.Skeleton;
        if (sk == null || it == null) return;
        // armour keeps the look it was made with (a knight's plate stays plate on the golem, whoever found it)
        var lookCls = ClassId.Soulbinder;
        if (it.Look != null && it.Look.StartsWith("arm_") && it.Look.IndexOf('_', 4) > 4) { int ci = Array.IndexOf(Names.ClassKeys, it.Look[4..it.Look.IndexOf('_', 4)]); if (ci >= 0) lookCls = (ClassId)ci; }
        string id = GearCatalog.ModelIdFor(it, lookCls);
        Node3D inst = id != null ? GearCatalog.Instantiate(id) : null;
        bool weapon = it.Kind is ItemKind.Weapon or ItemKind.OffHand;
        if (inst == null && weapon && it.Base != null) inst = GearPlaceholders.Weapon(it.Base, Math.Clamp(Tier(it.Power) / 2 + 1, 1, 3));
        if (inst == null) { Log.Warn("golem", $"no model for the forged {it.Name} ({id ?? it.Look ?? it.Base}): the golem keeps its plain look"); return; }
        foreach (var ap in inst.FindChildren("*", "AnimationPlayer", true, false).ToList()) ap.QueueFree();
        Color glow = it.Rarity >= Rarity.Legendary ? Palette.Rarity(it.Rarity) * 0.55f : it.Rarity == Rarity.Rare ? Palette.Rarity(it.Rarity) * 0.25f : Colors.Black;
        float bodyK = Mathf.Clamp(model.Height / 1.8f, 0.8f, 1.8f);
        string slot = it.Kind switch { ItemKind.Helm => "head", ItemKind.Chest => "torso", ItemKind.Gloves => "arm", ItemKind.Pants => "torso", ItemKind.Boots => "feet", _ => "arm" };
        string bone = weapon ? (it.Base == "shield" ? art.ArmL : art.ArmR) : slot switch { "head" => art.Head, "torso" => art.Torso, "feet" => "foot_r", _ => art.ArmR };
        int bi = model.BoneIndex(bone);
        if (bi < 0) bi = model.FindBoneLike(slot == "head" ? "head" : slot == "torso" ? "spine" : "hand");
        if (bi < 0) { inst.Free(); Log.Warn("golem", $"{model.ModelId} has no bone near '{bone}' for the forged {it.Name}"); return; }
        string boneName = sk.GetBoneName(bi);
        var att = new BoneAttachment3D { Name = "GolemFuse_" + boneName, BoneName = boneName };
        sk.AddChild(att);
        var holder = new Node3D { Name = "Fuse" };
        att.AddChild(holder);
        if (weapon)
        {
            string mount = it.Base == "shield" ? "forearm_l" : "hand_r";
            holder.Transform = GearGrips.For(it.Base, mount, sk);
            holder.Scale *= 1.35f;                                  // a golem-sized weapon: it becomes the striking arm
            holder.AddChild(inst);
            if (it.Base != "shield") model.ShowWeapons(false);
        }
        else
        {
            // armour is skinned to the hero's body: here it is a rigid plate, its rest shape centred on the bone and grown to the golem's girth
            holder.AddChild(inst);
            var box = GearCatalog.LocalBounds(inst, holder);
            var rest = sk.GetBoneGlobalRest(bi);
            float span = Mathf.Max(0.05f, Mathf.Max(box.Size.X, box.Size.Z));
            float want = (slot == "head" ? 0.5f : slot == "torso" ? 1.05f : 0.36f) * bodyK;
            float s = Mathf.Clamp(want / span, 0.3f, 3f);
            // a plate sits on the body's surface (front of the torso, on top of the head), not inside it
            var lift = slot == "torso" ? new Vector3(0f, 0f, 0.22f * bodyK) : slot == "head" ? new Vector3(0f, 0.12f * bodyK, 0.04f * bodyK) : Vector3.Zero;
            var t = new Transform3D(Basis.Identity.Scaled(Vector3.One * s), rest.Origin - box.GetCenter() * s + lift);
            holder.Transform = rest.AffineInverse() * t;
            foreach (var mi in inst.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                mi.Skin = null; mi.Skeleton = new NodePath();
            }
        }
        foreach (var mi in inst.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            CharModel.ConvertForWorld(mi);
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            mi.ExtraCullMargin = 1.5f;
            if (glow != Colors.Black) mi.SetInstanceShaderParameter("rim_color", new Vector3(glow.R, glow.G, glow.B));
        }
        // the slag weld where the item joins the body: a dull forge-orange seam (placeholder for the art's weld material)
        var seam = new MeshInstance3D
        {
            Name = "WeldSeam",
            Mesh = new TorusMesh { InnerRadius = 0.06f * bodyK, OuterRadius = 0.11f * bodyK, Rings = 12, RingSegments = 8 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.08f, 0.04f), EmissionEnabled = true, Emission = new Color(0.9f, 0.35f, 0.08f), EmissionEnergyMultiplier = 0.6f, Roughness = 0.9f },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        att.AddChild(seam);
        FuseReport = $"{id ?? "placeholder"} on {boneName} ({(weapon ? "weapon" : slot)})";
        Log.Info("golem", $"fused {it.Name} ({FuseReport}) onto {model.ModelId}");
    }

    /// <summary>For the test suite: what the last fuse attached where.</summary>
    public static string FuseReport = "";
}
