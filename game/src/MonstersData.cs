using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshenAbyss;

// The twelve kinds after Boss come from BESTIARY.md 5.1 (their behaviour lives in MonsterAi*.cs); they are appended so
// the values of the first nine never change.
public enum MonAI { Melee, Ranged, Caster, Exploder, Teleporter, Brute, Support, Leaper, Boss,
    Charger, Shieldbearer, Burrower, Bomber, Healer, Reviver, Spawner, Caller, Swarm, Turret, Flyer, Stalker }
public enum MonTier { Normal, Champion, Elite, Boss, Minion }

/// <summary>Behaviour flags that combine with any AI kind (BESTIARY.md 5.2).</summary>
[System.Flags]
public enum MonTrait
{
    None = 0, Dormant = 1 << 0, Hook = 1 << 1, Grab = 1 << 2, Breath = 1 << 3, Eruption = 1 << 4, Shell = 1 << 5, Reform = 1 << 6,
    Flammable = 1 << 7, Aura = 1 << 8, SplitOnDeath = 1 << 9, DeathBurst = 1 << 10, Escort = 1 << 11, Absorb = 1 << 12, Drain = 1 << 13,
    Extinguish = 1 << 14, Pulse = 1 << 15, Snare = 1 << 16, Return = 1 << 17, Trail = 1 << 18, Empower = 1 << 19, FrontArmour = 1 << 20,
    Phasing = 1 << 21, Reflect = 1 << 22,
}

/// <summary>
/// Where a type stands in a pack: cheap bodies, the line, the back line (ranged, casters, bombers, flyers, stalkers,
/// burrowers), heavies, one special at most (healer, reviver, mother, caller, aura or shield support), giants (one
/// per screen), things the layout places rather than walks (turrets, dormant ambushers) and swarm clouds.
/// </summary>
public enum PackSlot { Swarmer, Front, Back, Heavy, Special, Giant, Placed, Cloud }

/// <summary>A ground area a monster's attack leaves behind (bombs, trails, death patches).</summary>
public sealed class MonZone
{
    public float Radius = 2.4f, Dur = 4f, Mul = 0.15f;
    public Element El = Element.Fire;
    public StatusApply? Status;
    public float RootOnReentry;      // tallow pool: a second entry roots
}

/// <summary>
/// The data fields of the new AI kinds and traits (BESTIARY.md section 5), with the specification's defaults. Per-type
/// values are set in MonsterAiData.cs. One instance per definition; reskins get their own copy.
/// </summary>
public sealed class MonAiData
{
    // kinds
    public float ChargeSpeed = 14f, ChargeLen = 14f; public StatusApply? ChargeStatus;
    public float GuardArc = 120f, GuardDr = 0.15f, ReflectPct = 0.5f;
    public float BurrowSpeed = 6f, SurfaceTime = 5f; public bool StationaryEmerge; public StatusApply? EmergeStatus;
    public float BombRadius = 2.4f; public MonZone BombZone; public StatusApply? BombDirect; public bool BombMelee;
    public float HealPct = 0.06f; public string HealFilter;                 // null = anyone, "construct", or a family id
    public string[] ReviveFamilies;
    public string SpawnDef; public int SpawnCount = 2, SpawnCap = 6, SpawnOnDeath = 3; public float SpawnEvery = 6f; public bool EggSac;
    public string CallDef; public int CallCount = 3, CallCap = 6; public StatusApply? CallWail; public float CallWailRadius = 4f;
    public float SwarmRadius = 1.6f; public string Attract;
    public float TurnRate = 60f; public bool Shutter; public string Attack2 = "cone";     // flash | cloud | fan
    public float SwoopLen = 11f, OrbitDist = 8f;
    public float StalkDist = 7f, RevealTime = 3f; public StatusApply? PounceStatus;
    // traits
    public string DormantMode = "buried"; public float WakeRadius = 5.5f; public StatusApply? WakeStatus;
    public float HookRange = 9f; public StatusApply? HookStatus, HookStatus2;
    public float HoldTime = 1.5f; public bool GrabThrow;
    public float BreathLen = 7f, BreathArc = 50f; public string BreathMode = "status";    // chill | knock | status
    public StatusApply? BreathStatus;
    public float EruptRadius = 2.2f; public MonZone EruptZone;
    public float ShellMul = 1f;
    public float ReformTime = 4f;
    public StatusApply? LitBurn;
    public float AuraRadius = 8f; public string AuraEffect;                  // haste | windup | chill | weaken
    public string SplitDef; public int SplitCount = 3;
    public float BurstRadius = 2f, BurstDmg = 0.7f, BurstDelay = 0.6f; public StatusApply? BurstStatus; public MonZone BurstZone; public Element? BurstEl;
    public string[] EscortDefs;
    public float AbsorbRadius = 4f;
    public float DrainPct = 0.10f; public bool DrainHeal;
    public int PulseRings = 1; public StatusApply? PulseStatus, PulseNear; public float PulseNearRadius = 3f;
    public StatusApply? SnareRoot;
    public MonZone Trail;
    public float ArmourArc = 120f;
    // clips for the specials (logical names; CharModel maps them onto a creature's own clip set)
    public string CastAnim = "Spell_Simple_Shoot", MeleeAnim;

    public MonAiData Clone() => (MonAiData)MemberwiseClone();
}

/// <summary>Role archetypes inside a monster family: swarmer, melee, bruiser, ranged, caster, support.</summary>
public sealed class MonsterDef
{
    public string Id, Name, Model, Family, Sfx, Role = "melee";
    public float Scale = 1f;
    public MonAI AI = MonAI.Melee;
    public float Hp = 1f, Dmg = 1f, Xp = 1f;      // multipliers on the level curve
    public float Speed = 4.2f;
    public float Range = 2.2f;                    // attack reach / preferred distance for ranged
    public string[] AttackAnims = { "Sword_Regular_A" };
    public float AnimSpeed = 1f, Contact = 0.4f, Cooldown = 1.6f;
    public string Idle = "Idle_Loop", Walk = "Walk_Loop", Run = "Jog_Fwd_Loop";
    public Element Element = Element.Physical;
    public float ProjSpeed = 16f;
    public float Radius = 0.5f;                   // collision radius before scale
    public Color Tint = new(1, 1, 1);
    public bool Spectral, RiseFromGround;
    public float Poise = 1f;                      // resistance to hit-flinch
    public StatusApply? OnHit;                    // status its attacks apply
    public StatusApply? OnHit2;                   // a second status on its melee and area hits (Monster.Strike)

    // ── bestiary (filled by Bestiary.cs from data/bestiary.json; hand-written definitions get them merged in) ──
    public MonTrait Traits;
    public MonAiData Ai = new();
    public string NameEn = "", Rig = "A", BodyPlan = "humanoid biped", RoleText = "", Look = "";
    public string BaseId;                         // the type this one is a regional reskin of (null for regular types)
    public string RegionId;                       // a reskin's region
    public string[] Regions = System.Array.Empty<string>();
    public float SpawnWeight = 1f;
    public int Stage;
    public bool InBestiary;
    public PackSlot Slot = PackSlot.Front;        // its place in a pack (Populate)
    public bool Is(MonTrait t) => (Traits & t) != 0;
    public bool IsReskin => BaseId != null;
    /// <summary>Kinds that never lead a pack or carry elite affixes (BESTIARY.md 0: exploders, swarms, turrets, mothers, support roles).</summary>
    public bool CanLead => AI is not (MonAI.Exploder or MonAI.Swarm or MonAI.Turret or MonAI.Spawner or MonAI.Healer or MonAI.Reviver or MonAI.Caller or MonAI.Boss)
        && Role != "support" && Family != "hauler";
    public bool Giant => Scale >= 1.75f;

    public MonsterDef Variant(string id, string name, Color tint, System.Action<MonsterDef> f = null)
    {
        var m = (MonsterDef)MemberwiseClone();
        m.Id = id; m.Name = name; m.Tint = tint;
        m.Ai = Ai.Clone();
        f?.Invoke(m);
        return m;
    }
}

public sealed class EliteAffix
{
    public string Id, Name, Desc, Group;
    public Color Color;
}

public static class MonsterDb
{
    public static readonly List<MonsterDef> All = Build();
    private static readonly Dictionary<string, MonsterDef> ById = All.ToDictionary(m => m.Id);
    public static MonsterDef Get(string id) => id != null && ById.TryGetValue(id, out var m) ? m : null;

    private static List<MonsterDef> Build()
    {
        string[] zombie = { "Zombie_Scratch" };
        var l = new List<MonsterDef>();
        MonsterDef A(MonsterDef m) { l.Add(m); return m; }

        // ── 骸骨 Bone ──
        var skeleton = A(new MonsterDef { Id = "skeleton", Name = "骸骨兵", Model = "mon_skeleton", Family = "bone", Sfx = "skeleton", Hp = 0.9f, Speed = 3.9f,
            AttackAnims = new[] { "Sword_Regular_A", "Sword_Regular_B" }, Contact = 0.36f, Cooldown = 1.7f, RiseFromGround = true });
        var thrower = A(new MonsterDef { Id = "skeleton_thrower", Name = "骸骨擲矛手", Model = "mon_skeleton_thrower", Family = "bone", Sfx = "skeleton", Role = "ranged", AI = MonAI.Ranged, Hp = 0.7f, Dmg = 0.9f, Speed = 3.6f, Range = 13f,
            AttackAnims = new[] { "OverhandThrow" }, AnimSpeed = 1.1f, Contact = 0.55f, Cooldown = 2.6f, ProjSpeed = 19f, RiseFromGround = true });
        // ── 飢骸 Husks ──
        var husk = A(new MonsterDef { Id = "husk", Name = "飢骸", Model = "mon_husk", Family = "husk", Sfx = "husk", Role = "swarmer", Hp = 0.6f, Dmg = 0.75f, Speed = 5.6f, Range = 1.9f,
            AttackAnims = zombie, AnimSpeed = 1.5f, Contact = 0.4f, Cooldown = 1.1f, Idle = "Zombie_Idle_Loop", Walk = "Zombie_Walk_Fwd_Loop", Poise = 0.6f });
        var bloated = A(new MonsterDef { Id = "husk_bloated", Name = "腫脹飢骸", Model = "mon_husk_bloated", Family = "husk", Sfx = "husk", Role = "bruiser", AI = MonAI.Exploder, Hp = 1.5f, Dmg = 2.4f, Speed = 2.3f, Range = 2.2f, Scale = 1.15f,
            AttackAnims = zombie, Contact = 0.6f, Cooldown = 2.2f, Idle = "Zombie_Idle_Loop", Walk = "Zombie_Walk_Fwd_Loop", Run = "Zombie_Walk_Fwd_Loop", Element = Element.Poison, Xp = 1.4f, Poise = 2f });
        // ── 寂滅教團 Hushed Order ──
        var cultist = A(new MonsterDef { Id = "cultist", Name = "寂滅教徒", Model = "mon_cultist", Family = "cult", Sfx = "cultist", Hp = 0.85f, Dmg = 1.05f, Speed = 4.6f,
            AttackAnims = new[] { "Sword_Regular_A", "Sword_Regular_C", "Melee_Hook" }, AnimSpeed = 1.2f, Contact = 0.32f, Cooldown = 1.4f });
        var caster = A(new MonsterDef { Id = "cult_caster", Name = "寂滅祭司", Model = "mon_cult_caster", Family = "cult", Sfx = "cultist", Role = "caster", AI = MonAI.Caster, Hp = 0.75f, Dmg = 1.15f, Speed = 3.4f, Range = 12f,
            AttackAnims = new[] { "Spell_Simple_Shoot" }, Contact = 0.4f, Cooldown = 2.4f, Element = Element.Shadow, ProjSpeed = 13f, Xp = 1.2f });
        var knight = A(new MonsterDef { Id = "knight", Name = "墮落騎士", Model = "mon_knight", Family = "cult", Sfx = "brute", Role = "bruiser", AI = MonAI.Brute, Hp = 2.6f, Dmg = 1.7f, Speed = 3.6f, Range = 2.8f, Scale = 1.15f,
            AttackAnims = new[] { "Sword_Attack", "Sword_Heavy_Combo" }, AnimSpeed = 0.95f, Contact = 0.6f, Cooldown = 2.3f, Xp = 2.2f, Poise = 4f, OnHit = St.Stun(0.6f) });
        var choir = A(new MonsterDef { Id = "choir", Name = "靜默唱詩者", Model = "mon_choir", Family = "cult", Sfx = "choir", Role = "support", AI = MonAI.Support, Hp = 0.9f, Dmg = 0.8f, Speed = 3.0f, Range = 11f,
            AttackAnims = new[] { "Spell_Simple_Shoot" }, Contact = 0.4f, Cooldown = 2.8f, Element = Element.Shadow, ProjSpeed = 11f, Spectral = true, Xp = 1.6f });
        // ── 縛魂 Wraith ──
        var wraith = A(new MonsterDef { Id = "wraith", Name = "縛魂", Model = "mon_wraith", Family = "wraith", Sfx = "wraith", Role = "caster", AI = MonAI.Teleporter, Hp = 0.8f, Dmg = 1.2f, Speed = 3.2f, Range = 11f,
            AttackAnims = new[] { "Spell_Simple_Shoot" }, Contact = 0.4f, Cooldown = 2.2f, Element = Element.Frost, ProjSpeed = 12f, Spectral = true, Xp = 1.3f, OnHit = St.Chill(0.3f) });
        // ── 燼魔 Ember fiends ──
        var fiend = A(new MonsterDef { Id = "fiend", Name = "燼魔", Model = "mon_fiend", Family = "fiend", Sfx = "fiend", Hp = 1.2f, Dmg = 1.25f, Speed = 4.8f, Range = 2.3f,
            AttackAnims = new[] { "Melee_Hook", "Punch_Cross", "Zombie_Scratch" }, AnimSpeed = 1.2f, Contact = 0.34f, Cooldown = 1.4f, Element = Element.Fire, Xp = 1.3f, OnHit = St.Burn(0.3f, 3) });
        var imp = A(new MonsterDef { Id = "imp", Name = "小燼魔", Model = "mon_fiend", Family = "fiend", Sfx = "fiend", Role = "ranged", AI = MonAI.Ranged, Scale = 0.6f, Hp = 0.45f, Dmg = 0.7f, Speed = 5.4f, Range = 10f,
            AttackAnims = new[] { "OverhandThrow" }, AnimSpeed = 1.5f, Contact = 0.4f, Cooldown = 1.9f, Element = Element.Fire, ProjSpeed = 15f, Xp = 0.7f, Poise = 0.5f });
        var brute = A(new MonsterDef { Id = "brute", Name = "熔岩巨魔", Model = "mon_brute", Family = "fiend", Sfx = "brute", Role = "bruiser", AI = MonAI.Brute, Scale = 1.5f, Hp = 3.4f, Dmg = 2f, Speed = 3.1f, Range = 3.4f,
            AttackAnims = new[] { "Sword_Attack", "Sword_Heavy_Combo" }, AnimSpeed = 0.85f, Contact = 0.7f, Cooldown = 2.6f, Element = Element.Fire, Xp = 2.8f, Poise = 6f });
        // ── 霜縛 Frostbound ──
        A(new MonsterDef { Id = "frostbound", Name = "霜縛屍兵", Model = "mon_frostbound", Family = "frost", Sfx = "skeleton", Hp = 1.0f, Dmg = 1.05f, Speed = 3.8f, Range = 2.8f,
            AttackAnims = new[] { "Sword_Regular_A", "Sword_Regular_B" }, Contact = 0.38f, Cooldown = 1.8f, Element = Element.Frost, OnHit = St.Chill(0.25f) });
        A(new MonsterDef { Id = "rime_stalker", Name = "霜角獵者", Model = "mon_rime_stalker", Family = "frost", Sfx = "stalker", Role = "swarmer", AI = MonAI.Leaper, Hp = 0.7f, Dmg = 0.9f, Speed = 6.0f, Range = 2.0f,
            AttackAnims = new[] { "Zombie_Scratch", "Melee_Hook" }, AnimSpeed = 1.5f, Contact = 0.32f, Cooldown = 1.2f, Element = Element.Frost, Poise = 0.7f });
        A(wraith.Variant("ice_wraith", "霜魂", new Color(0.6f, 0.85f, 1f)));
        A(thrower.Variant("frost_thrower", "霜骸擲矛手", new Color(0.65f, 0.85f, 1f), m => { m.Element = Element.Frost; m.OnHit = St.Chill(0.25f); }));
        A(knight.Variant("frost_knight", "冰封騎士", new Color(0.55f, 0.8f, 1f), m => { m.Element = Element.Frost; m.OnHit = St.Chill(0.5f); }));
        // ── 沼裔 Mireborn ──
        A(new MonsterDef { Id = "mire_hag", Name = "沼巫", Model = "mon_mire_hag", Family = "mire", Sfx = "hag", Role = "caster", AI = MonAI.Caster, Hp = 0.85f, Dmg = 1.1f, Speed = 3.2f, Range = 12f,
            AttackAnims = new[] { "Spell_Simple_Shoot" }, Contact = 0.4f, Cooldown = 2.5f, Element = Element.Poison, ProjSpeed = 12f, Xp = 1.3f, OnHit = St.Poison(0.4f, 4) });
        A(new MonsterDef { Id = "rotspawn", Name = "腐泥巨怪", Model = "mon_rotspawn", Family = "mire", Sfx = "rotspawn", Role = "bruiser", AI = MonAI.Brute, Scale = 1.4f, Hp = 3.2f, Dmg = 1.9f, Speed = 2.8f, Range = 3.2f,
            AttackAnims = new[] { "Sword_Attack", "Punch_Cross" }, AnimSpeed = 0.8f, Contact = 0.7f, Cooldown = 2.7f, Element = Element.Poison, Xp = 2.6f, Poise = 6f, Idle = "Zombie_Idle_Loop", Walk = "Zombie_Walk_Fwd_Loop", Run = "Zombie_Walk_Fwd_Loop" });
        A(husk.Variant("mire_husk", "沼屍", new Color(0.6f, 0.8f, 0.5f), m => { m.Element = Element.Poison; m.OnHit = St.Poison(0.2f, 3); }));
        A(bloated.Variant("mire_bloated", "脹氣沼屍", new Color(0.65f, 0.85f, 0.45f)));
        A(thrower.Variant("mire_thrower", "沼骸擲矛手", new Color(0.6f, 0.8f, 0.5f), m => { m.Element = Element.Poison; m.OnHit = St.Poison(0.25f, 3); }));
        // ── 砂葬 Sandbound ──
        A(new MonsterDef { Id = "sand_revenant", Name = "砂葬者", Model = "mon_sand_revenant", Family = "sands", Sfx = "revenant", Hp = 0.85f, Dmg = 1.1f, Speed = 5.0f, Range = 2.3f,
            AttackAnims = new[] { "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C" }, AnimSpeed = 1.35f, Contact = 0.28f, Cooldown = 1.2f, OnHit = St.Bleed(0.3f, 3) });
        A(new MonsterDef { Id = "brass_sentinel", Name = "黃銅守衛", Model = "mon_brass_sentinel", Family = "sands", Sfx = "construct", Role = "bruiser", AI = MonAI.Brute, Scale = 1.3f, Hp = 3.0f, Dmg = 1.8f, Speed = 3.0f, Range = 3.4f,
            AttackAnims = new[] { "Sword_Attack", "Sword_Heavy_Combo" }, AnimSpeed = 0.85f, Contact = 0.65f, Cooldown = 2.5f, Element = Element.Lightning, Xp = 2.6f, Poise = 8f });
        A(caster.Variant("sand_caster", "砂語祭司", new Color(1f, 0.85f, 0.55f), m => m.Element = Element.Lightning));
        A(thrower.Variant("sand_thrower", "砂骸擲矛手", new Color(1f, 0.88f, 0.6f)));
        A(husk.Variant("sand_husk", "乾屍", new Color(1f, 0.85f, 0.6f)));
        // ── variants reused in other regions ──
        A(knight.Variant("ember_knight", "燼騎士", new Color(1f, 0.6f, 0.4f), m => { m.Element = Element.Fire; m.OnHit = St.Burn(0.4f, 3); }));

        // ── region bosses ──
        MonsterDef Boss(string id, string name, string model, float scale, float hp, float dmg, float range, Element el, float xp, params string[] anims) =>
            A(new MonsterDef { Id = id, Name = name, Model = model, Family = "boss", Sfx = "boss", Role = "boss", AI = MonAI.Boss, Scale = scale, Hp = hp, Dmg = dmg, Speed = 3.6f, Range = range,
                AttackAnims = anims, AnimSpeed = 0.85f, Contact = 0.65f, Cooldown = 2.1f, Element = el, Xp = xp, Poise = 99f, Radius = 0.6f });
        Boss("boss_gravedigger", "掘墓者・葛魯姆", "boss_gravedigger", 1.9f, 38f, 1.9f, 4.2f, Element.Physical, 40f, "Sword_Attack", "Sword_Regular_A");
        Boss("boss_rimelord", "霜骸領主・赫爾蒙", "boss_rimelord", 2.0f, 42f, 2.0f, 4.4f, Element.Frost, 45f, "Sword_Attack", "Sword_Heavy_Combo");
        Boss("boss_bishop", "腐骨主教・瑪爾薩", "boss_bishop", 1.8f, 44f, 2.0f, 14f, Element.Shadow, 50f, "Spell_Simple_Shoot");
        Boss("boss_mire_mother", "沼母・艾絲珂", "boss_mire_mother", 2.1f, 48f, 2.1f, 13f, Element.Poison, 55f, "Spell_Simple_Shoot", "Melee_Hook");
        Boss("boss_sand_tyrant", "鏽砂暴君・伊薩克", "boss_sand_tyrant", 2.3f, 54f, 2.2f, 5f, Element.Lightning, 60f, "Sword_Attack", "Sword_Regular_C");
        Boss("boss_grand_hush", "寂滅大導師・賽倫", "boss_grand_hush", 2.1f, 58f, 2.3f, 12f, Element.Shadow, 70f, "Spell_Simple_Shoot", "Sword_Attack");
        Boss("boss_osaros", "寂主・奧薩洛斯", "boss_osaros", 2.4f, 95f, 2.5f, 5f, Element.Shadow, 100f, "Sword_Attack", "Sword_Regular_A", "Sword_Regular_C");

        // ── player-side summons ──
        var teal = new Color(0.55f, 0.95f, 0.85f);
        A(skeleton.Variant("minion_skeleton", "骸骨僕從", teal, m => { m.Family = "minion"; m.Speed = 5.8f; m.AnimSpeed = 1.2f; m.Cooldown = 1.3f; m.Contact = 0.32f; }));
        A(thrower.Variant("minion_thrower", "骸骨擲矛手", teal, m => { m.Family = "minion"; m.Speed = 5.4f; m.AnimSpeed = 1.3f; m.Cooldown = 1.9f; m.Range = 12f; }));
        A(brute.Variant("minion_colossus", "骸骨巨像", teal, m => { m.Family = "minion"; m.Scale = 1.6f; m.Hp = 8f; m.Dmg = 1f; m.Speed = 4.4f; m.Cooldown = 2f; m.Element = Element.Physical; m.RiseFromGround = true; }));
        var pact = new Color(1f, 0.55f, 0.7f);
        A(brute.Variant("pact_brute", "契約巨魔", pact, m => { m.Family = "minion"; m.Scale = 1.3f; m.Hp = 6f; m.Dmg = 1f; m.Speed = 4.6f; m.Cooldown = 2f; }));
        A(imp.Variant("pact_imp", "契約小魔", pact, m => { m.Family = "minion"; m.Hp = 1.2f; m.Dmg = 0.45f; m.Cooldown = 1.5f; }));
        A(wraith.Variant("pact_wraith", "契約縛魂", pact, m => { m.Family = "minion"; m.Hp = 2.5f; m.Dmg = 0.9f; m.Cooldown = 1.7f; m.AI = MonAI.Caster; }));
        A(fiend.Variant("pact_fiend", "契約燼魔", pact, m => { m.Family = "minion"; m.Hp = 3.5f; m.Dmg = 1f; m.Speed = 5.6f; m.Cooldown = 1.2f; }));
        A(brute.Variant("pact_gate_brute", "淵門巨魔", pact, m => { m.Family = "minion"; m.Scale = 1.7f; m.Hp = 10f; m.Dmg = 1f; m.Speed = 4.6f; m.Cooldown = 1.9f; }));
        // the designed roster: every regular type and regional reskin of data/bestiary.json joins the list; the
        // definitions above keep their tuned numbers and only gain family, traits, regions and weights
        try { Bestiary.Merge(l); }
        catch (System.Exception e) { Log.Error("bestiary", "merging the bestiary into the monster list failed; only the built-in definitions are available", e); }
        return l;
    }

    /// <summary>Elite affixes in four groups (hazard / control / defensive / behavioural), following the genre's pattern.</summary>
    public static readonly List<EliteAffix> Affixes = new()
    {
        new() { Id = "molten", Group = "hazard", Name = "熔火", Desc = "朝你投擲熔岩，死亡時爆炸。", Color = new Color(1f, 0.4f, 0.1f) },
        new() { Id = "frozen", Group = "hazard", Name = "冰封", Desc = "召喚會爆裂並凍結的冰晶。", Color = new Color(0.4f, 0.8f, 1f) },
        new() { Id = "shocking", Group = "hazard", Name = "雷殛", Desc = "受擊時向四周釋放閃電。", Color = new Color(0.8f, 0.75f, 1f) },
        new() { Id = "plagued", Group = "hazard", Name = "腐疫", Desc = "腳下不斷滲出腐蝕之池。", Color = new Color(0.55f, 0.75f, 0.2f) },
        new() { Id = "shadow", Group = "hazard", Name = "追影", Desc = "放出緩慢追蹤的暗影球。", Color = new Color(0.6f, 0.3f, 0.9f) },
        new() { Id = "waller", Group = "control", Name = "築牆", Desc = "升起石牆截斷退路。", Color = new Color(0.7f, 0.65f, 0.55f) },
        new() { Id = "vortex", Group = "control", Name = "渦引", Desc = "將你拉到牠的面前。", Color = new Color(0.5f, 0.6f, 0.9f) },
        new() { Id = "chilling", Group = "control", Name = "寒息", Desc = "周圍的寒氣會減緩你的行動。", Color = new Color(0.6f, 0.9f, 1f) },
        new() { Id = "shielded", Group = "defensive", Name = "護盾", Desc = "週期性張開擋下投射物的護罩。", Color = new Color(0.9f, 0.85f, 0.5f) },
        new() { Id = "armored", Group = "defensive", Name = "厚甲", Desc = "受到的傷害大幅降低，但行動遲緩。", Color = new Color(0.65f, 0.65f, 0.7f) },
        new() { Id = "vampiric", Group = "defensive", Name = "吸血", Desc = "攻擊命中時恢復生命。", Color = new Color(0.85f, 0.1f, 0.2f) },
        new() { Id = "hasted", Group = "behavioural", Name = "疾風", Desc = "移動與攻擊速度大幅提高。", Color = new Color(0.7f, 1f, 0.6f) },
        new() { Id = "summoner", Group = "behavioural", Name = "喚亡", Desc = "不斷喚起骸骨為其作戰。", Color = new Color(0.6f, 0.9f, 0.6f) },
        new() { Id = "teleporter", Group = "behavioural", Name = "閃現", Desc = "會瞬間移動到獵物身旁。", Color = new Color(0.7f, 0.4f, 1f) },
        new() { Id = "enraged", Group = "behavioural", Name = "狂怒", Desc = "生命低於三成時陷入狂暴。", Color = new Color(1f, 0.25f, 0.2f) },
        new() { Id = "splitting", Group = "behavioural", Name = "分裂", Desc = "死亡時分裂為兩隻較弱的個體。", Color = new Color(0.9f, 0.6f, 0.9f) },
    };
    public static EliteAffix Affix(string id) => Affixes.FirstOrDefault(a => a.Id == id);
}

public enum DungeonGoal { None, SlayElites, DestroyIdols, CollectEmbers, FreePrisoners }

public sealed class DungeonDef
{
    public string Id, Name, Theme;
    public DungeonGoal Goal;
    public bool Story;             // the region's story dungeon, ends in the region boss
    public string MiniBoss;        // side dungeons end with a named elite built from this monster
    public string[] Families;      // the two or three monster families that live here (bestiary.json); null = the region's own mix
}

/// <summary>A region: an overworld with a beacon stronghold, events, altars and dungeon entrances, plus its dungeons.</summary>
public sealed class RegionDef
{
    public string Id, Name, Subtitle, Theme, Music, Amb, Boss, Stronghold, StrongholdBoss;
    public int Act, Level;         // recommended level; monsters scale up to the player
    public (string id, float w)[] Spawns;        // flat (type, weight) list: family weight × type weight (bestiary.json; the lists below are the fallback)
    public (string id, float w, bool signature, string[] types)[] Families;   // weighted families and the types of each that live here
    public DungeonDef[] Dungeons;
    public Color Accent;
}

public static class ZoneDb
{
    private static (string, float)[] W(params (string, float)[] s) => s;

    // the region spawn tables and dungeon family lists come from data/bestiary.json; the lists in the initialiser below
    // stay as the fallback when that file is missing or broken
    static ZoneDb()
    {
        try { Bestiary.ApplyRegions(Regions); }
        catch (System.Exception e) { Log.Error("bestiary", "applying the bestiary's region tables failed; the built-in spawn lists are used", e); }
    }

    public static readonly List<RegionDef> Regions = new()
    {
        new() { Id = "wastes", Act = 1, Level = 1, Name = "灰燼荒原", Subtitle = "枯木與墳塚之地", Theme = "wastes", Music = "wastes", Amb = "amb_wastes", Boss = "boss_gravedigger",
            Stronghold = "斷燈哨站", StrongholdBoss = "husk_bloated", Accent = new Color(0.55f, 0.7f, 1f),
            Spawns = W(("husk", 5f), ("skeleton", 3f), ("skeleton_thrower", 1.6f), ("husk_bloated", 1f)),
            Dungeons = new DungeonDef[] {
                new() { Id = "wastes_d1", Name = "荒棄地窖", Theme = "crypt", Goal = DungeonGoal.SlayElites, MiniBoss = "skeleton" },
                new() { Id = "wastes_d2", Name = "枯根洞", Theme = "cave", Goal = DungeonGoal.DestroyIdols, MiniBoss = "husk_bloated" },
                new() { Id = "wastes_boss", Name = "掘墓者的墓園", Theme = "graveyard", Goal = DungeonGoal.None, Story = true } } },
        new() { Id = "frost", Act = 2, Level = 8, Name = "霜脊隘口", Subtitle = "被風雪掩埋的關隘", Theme = "frost", Music = "frost", Amb = "amb_frost", Boss = "boss_rimelord",
            Stronghold = "凍骨關", StrongholdBoss = "frost_knight", Accent = new Color(0.5f, 0.85f, 1f),
            Spawns = W(("frostbound", 4f), ("rime_stalker", 3.5f), ("frost_thrower", 2f), ("ice_wraith", 1.5f), ("frost_knight", 0.8f)),
            Dungeons = new DungeonDef[] {
                new() { Id = "frost_d1", Name = "冰封哨站", Theme = "icehall", Goal = DungeonGoal.FreePrisoners, MiniBoss = "frost_knight" },
                new() { Id = "frost_d2", Name = "霜牙洞窟", Theme = "icecave", Goal = DungeonGoal.CollectEmbers, MiniBoss = "rime_stalker" },
                new() { Id = "frost_boss", Name = "赫爾蒙的冰廳", Theme = "icehall", Goal = DungeonGoal.SlayElites, Story = true } } },
        new() { Id = "crypt", Act = 2, Level = 8, Name = "沉眠墓原", Subtitle = "寂滅教團褻瀆的陵寢", Theme = "barrows", Music = "crypt", Amb = "amb_crypt", Boss = "boss_bishop",
            Stronghold = "守陵人營地", StrongholdBoss = "knight", Accent = new Color(0.5f, 0.9f, 0.55f),
            Spawns = W(("skeleton", 4f), ("skeleton_thrower", 2f), ("cultist", 4f), ("cult_caster", 2f), ("wraith", 1.5f), ("knight", 0.8f)),
            Dungeons = new DungeonDef[] {
                new() { Id = "crypt_d1", Name = "無名陵寢", Theme = "crypt", Goal = DungeonGoal.DestroyIdols, MiniBoss = "knight" },
                new() { Id = "crypt_d2", Name = "骨倉", Theme = "crypt", Goal = DungeonGoal.CollectEmbers, MiniBoss = "cult_caster" },
                new() { Id = "crypt_boss", Name = "腐骨聖所", Theme = "crypt", Goal = DungeonGoal.SlayElites, Story = true } } },
        new() { Id = "mire", Act = 2, Level = 8, Name = "瘴泣沼澤", Subtitle = "連霧都在腐爛的地方", Theme = "mire", Music = "mire", Amb = "amb_mire", Boss = "boss_mire_mother",
            Stronghold = "沉木寨", StrongholdBoss = "rotspawn", Accent = new Color(0.75f, 0.9f, 0.3f),
            Spawns = W(("mire_husk", 4.5f), ("mire_bloated", 1f), ("mire_hag", 2f), ("rotspawn", 0.9f), ("mire_thrower", 2f)),
            Dungeons = new DungeonDef[] {
                new() { Id = "mire_d1", Name = "溺亡者地窟", Theme = "wetcave", Goal = DungeonGoal.FreePrisoners, MiniBoss = "rotspawn" },
                new() { Id = "mire_d2", Name = "沼巫巢穴", Theme = "wetcave", Goal = DungeonGoal.DestroyIdols, MiniBoss = "mire_hag" },
                new() { Id = "mire_boss", Name = "沼母之庭", Theme = "wetcave", Goal = DungeonGoal.CollectEmbers, Story = true } } },
        new() { Id = "sands", Act = 5, Level = 26, Name = "鏽砂廢都", Subtitle = "黃銅王朝的殘骸", Theme = "sands", Music = "sands", Amb = "amb_sands", Boss = "boss_sand_tyrant",
            Stronghold = "斷柱商站", StrongholdBoss = "brass_sentinel", Accent = new Color(0.8f, 0.6f, 1f),
            Spawns = W(("sand_revenant", 4f), ("sand_husk", 3f), ("sand_thrower", 2f), ("sand_caster", 1.8f), ("brass_sentinel", 0.9f)),
            Dungeons = new DungeonDef[] {
                new() { Id = "sands_d1", Name = "黃銅工坊", Theme = "brasshall", Goal = DungeonGoal.DestroyIdols, MiniBoss = "brass_sentinel" },
                new() { Id = "sands_d2", Name = "沙掩墓道", Theme = "sandtomb", Goal = DungeonGoal.SlayElites, MiniBoss = "sand_revenant" },
                new() { Id = "sands_boss", Name = "暴君王座", Theme = "brasshall", Goal = DungeonGoal.CollectEmbers, Story = true } } },
        new() { Id = "sanctum", Act = 6, Level = 33, Name = "寂滅聖城", Subtitle = "鐘聲早已停歇的聖都", Theme = "sanctum", Music = "sanctum", Amb = "amb_sanctum", Boss = "boss_grand_hush",
            Stronghold = "燭匠街壘", StrongholdBoss = "knight", Accent = new Color(0.95f, 0.95f, 1f),
            Spawns = W(("cultist", 4f), ("cult_caster", 2.2f), ("knight", 1.2f), ("choir", 1.4f), ("wraith", 1.2f)),
            Dungeons = new DungeonDef[] {
                new() { Id = "sanctum_d1", Name = "靜默修院", Theme = "cathedral", Goal = DungeonGoal.FreePrisoners, MiniBoss = "choir" },
                new() { Id = "sanctum_d2", Name = "鐘樓地庫", Theme = "crypt", Goal = DungeonGoal.CollectEmbers, MiniBoss = "knight" },
                new() { Id = "sanctum_boss", Name = "無聲大聖堂", Theme = "cathedral", Goal = DungeonGoal.DestroyIdols, Story = true } } },
        new() { Id = "abyss", Act = 7, Level = 40, Name = "燼淵", Subtitle = "熄滅日爐之下", Theme = "abyss", Music = "abyss", Amb = "amb_abyss", Boss = "boss_osaros",
            Stronghold = "餘火前哨", StrongholdBoss = "brute", Accent = new Color(1f, 0.5f, 0.15f),
            Spawns = W(("fiend", 4f), ("imp", 3.5f), ("brute", 1f), ("ember_knight", 1.2f), ("cult_caster", 1.5f), ("wraith", 1.2f)),
            Dungeons = new DungeonDef[] {
                new() { Id = "abyss_d1", Name = "熔爐遺構", Theme = "forge", Goal = DungeonGoal.DestroyIdols, MiniBoss = "brute" },
                new() { Id = "abyss_d2", Name = "黑曜裂谷", Theme = "lavacave", Goal = DungeonGoal.SlayElites, MiniBoss = "ember_knight" },
                new() { Id = "abyss_boss", Name = "日爐之心", Theme = "forge", Goal = DungeonGoal.FreePrisoners, Story = true } } },
    };

    public const string Town = "town", TownName = "殘燈鎮", TownSubtitle = "最後的燼火庇護所";
    public static RegionDef Region(string id) => Regions.FirstOrDefault(r => r.Id == id);
    public static RegionDef RegionOf(string areaId) => Regions.FirstOrDefault(r => r.Id == areaId || r.Dungeons.Any(d => d.Id == areaId));
    public static DungeonDef Dungeon(string areaId) => Regions.SelectMany(r => r.Dungeons).FirstOrDefault(d => d.Id == areaId);
    public static string AreaName(string areaId) =>
        areaId == Town ? TownName : areaId.StartsWith("rift") ? "深淵裂隙" : Dungeon(areaId)?.Name ?? Region(areaId)?.Name ?? areaId;

    public static readonly string[] GoalText =
    {
        "", "討伐被標記的菁英", "摧毀寂滅圖騰", "收集燼晶並獻於祭壇", "解救被囚的倖存者",
    };

    public static readonly string[] TierNames = { "冒險者", "老練者", "夢魘", "煉獄" };
    public static readonly string[] TierDesc =
    {
        "適合初次踏入瑟蘭的持燼者。",
        "怪物更強悍。經驗值 +40%，金幣 +25%，掉落品質提高。",
        "怪物的生命與傷害大幅提高。經驗值 +100%，傳奇掉落率提高，開始掉落神話之物。",
        "唯有真正的持燼者才能生還。經驗值 +180%，獨特物品掉落率大幅提高。",
    };
}

/// <summary>Level curves. Monster life/damage derive from the damage and life a player is expected to have at that level.</summary>
public static class Balance
{
    public const int LevelCap = 50;
    public const int ParagonCap = 150;
    /// <summary>
    /// Experience for the next level. Tuned from bot runs (about 30 kills a minute, 1.4 normal kills of experience per kill
    /// once champions and elites are counted): 15 kills for level 2, 40 for level 4, then 60-100 — one level every two to
    /// four minutes through the campaign. (The first curve, 70 + 38·L^1.8, gave a level every 40 s at level 10.)
    /// </summary>
    public static long XpToNext(int level)
    {
        float xp = 300f + 520f * System.MathF.Pow(level, 1.15f);
        if (level < 5) xp *= 0.35f + 0.13f * level;         // the first levels come quickly
        return (long)System.MathF.Round(xp);
    }
    public static long ParagonXp(int plevel) => (long)(XpToNext(LevelCap) * 0.3f * (1f + plevel * 0.02f));
    public static float MonsterXp(int level) => 12 + 6 * level;
    public const float MiniBossXp = 24f;          // × a normal kill of its level
    /// <summary>
    /// Sustained single-target damage per second of a hero in rare gear of that level. The last factor is fitted to
    /// measurements (--suite=dps and boss runs): a fresh hero does a little less than the first estimate (0.9× at
    /// level 5), then gear affixes, ranks, upgrades and passives stack faster than it assumed — 1.4× at level 25, 2× at 50.
    /// Below level 30 the factor is lower still: a hero who plays the story from level 1 wears what dropped (a few magic
    /// pieces some levels old, not nine rares) and did 0.55× the first estimate at level 4-10 — 6 s per normal monster.
    /// </summary>
    public static float ExpectedDps(int level) =>
        ItemDb.WeaponAvg(level * 10) * 1.4f * (1f + 0.03f * level) * (1f + 0.012f * level) * System.Math.Clamp(0.56f + 0.032f * level, 0.56f, 2f);
    public static float MonsterHp(int level) => ExpectedDps(level) * 2.2f;
    // A fresh hero has a starter weapon and no armour: at level 1 a hit used to cost 10 % of their life and a pack of husks
    // killed a (bot) hunter 8 times before level 4. Softer first levels (hit 8.3 instead of 11.2, life 154 instead of 114);
    // from level 20 on the numbers are within 3 % of the old ones.
    public static float MonsterDmg(int level) => 5f + 3.3f * level;
    public static float BaseLife(int level) => 130f + 24f * level;
    public static float ArmorDr(float armor, int attackerLevel) => System.MathF.Min(0.8f, armor / (armor + 100f + 40f * attackerLevel));
    public static readonly float[] TierHp = { 1f, 1.7f, 3.2f, 6.5f };
    public static readonly float[] TierDmg = { 1f, 1.45f, 2.3f, 3.6f };
    public static readonly float[] TierXp = { 1f, 1.4f, 2f, 2.8f };
    public static readonly float[] TierGold = { 1f, 1.25f, 1.6f, 2.2f };
    public static readonly int[] TierLevelBonus = { 0, 2, 4, 6 };
    /// <summary>Monsters match the player's level but never drop below the region's own level.</summary>
    public static int AreaLevel(int regionLevel, int playerLevel, int tier, bool dungeon) =>
        System.Math.Max(regionLevel, playerLevel) + TierLevelBonus[tier] + (dungeon ? 1 : 0);
}
