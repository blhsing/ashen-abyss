using Godot;
using System;
using System.Collections.Generic;
using MoveK = AshenAbyss.BossMoveKind;

namespace AshenAbyss;

/// <summary>The move library: every boss attack is one of these primitives, tuned by a <see cref="BossMoveDef"/>.</summary>
internal enum BossMoveKind
{
    Cone,        // frontal cone slam; Count > 1 makes it a combo of re-aimed swings
    Slam,        // circle around the boss; Rings adds shock rings that roll outward, Shots a radial burst of bolts
    Charge,      // a marked lane the boss then travels along
    Line,        // beams from the boss; Count lines in a fan
    Eruptions,   // circles under the hero in sequence; may leave a zone behind
    Donut,       // ring around the boss that is safe close to it (Radius 0 = out to the arena wall)
    Fan,         // projectile fan, optionally in several volleys
    Spiral,      // channelled spiral of slow projectiles
    Homing,      // slow orbs that curve after the hero
    Cross,       // lines crossing at the boss (or at the hero), optionally stepping round
    Rain,        // random circles over the arena for a while
    Summon,      // adds from named defs or the level's region table
    Teleport,    // blink to another spot of the arena
    TeleStrike,  // vanish, then reappear on a marked circle with a slam
    PullSlam,    // a tether that drags the hero in, followed by a slam
    Leap,        // jump to a marked circle
    Prison,      // rings of circles closing on where the hero stood
}

/// <summary>One entry of a boss's move list. Which fields matter depends on <see cref="Kind"/>.</summary>
internal sealed class BossMoveDef
{
    public string Id;
    public MoveK Kind;
    public int Phase = 1;                       // first phase that may use the move
    public float Cd = 10f, First = -1f;         // cooldown; delay before the first use (−1 = a random share of the cooldown)
    public float MinRange, MaxRange = 60f, Weight = 1f;
    public float Wind = 0.9f;                   // how long the telegraph fills (never played faster than 0.6 s)
    public float Recover = 0.7f, Interval = 0.7f, Duration = 3f;
    public string Anim = "Sword_Attack";
    public string[] Anims;                      // cycled per swing / step instead of Anim
    public float Contact = 0.5f;                // seconds into the animation (at speed 1) at which the blow lands
    public Element El = Element.Physical;
    public StatusApply? Status, ZoneStatus;
    public float Dmg = 1f, RingDmg = 0.9f, ZoneDmg = 0.15f, ZoneDur;
    public float Radius = 4f, Inner = 4f, Arc = 100f, Length = 14f, Width = 2.4f, RingWidth = 3f;
    public int Count = 1, Waves = 1, Rings, Shots;
    public float PerPhase, WavesPerPhase, RingsPerPhase;      // growth per phase after the unlock phase
    public float Spread = 14f, Speed = 10f, Turn, Reach = 22f, ShotRadius = 0.45f, Rotate = 30f;
    public string[] Adds;                       // null = the level's region table
    public string Sfx, HitSfx, Warn;
    public bool AtHero, Away, Meteor;
    public BossMoveDef Clone() => (BossMoveDef)MemberwiseClone();
}

/// <summary>A boss's identity: stance, basic attack, move list, phase lines and add waves.</summary>
internal sealed class BossKit
{
    public string Id, Short, EnrageLine;
    public Element El = Element.Physical;
    public bool Ranged;
    public float KeepMin = 6f, KeepMax = 12.5f;
    public BossMoveDef Basic;
    public BossMoveDef[] Moves = Array.Empty<BossMoveDef>();
    public string[] PhaseLines = Array.Empty<string>();       // spoken when phase 2 and phase 3 begin
    public string[] WaveAdds;                                  // null = the level's region table
    public int WaveCount = 3;
    public float[] Haste = { 1f, 1.12f, 1.25f };
    public float[] Gcd = { 1.6f, 1.2f, 0.8f };                 // breathing room after a special, per phase
    // ── named encounters (Named*.cs): stronghold / dungeon / world / pinnacle bosses run on the same brain ──
    public bool NoRoar;                 // phases still speed up and unlock moves, without the roar, the nova, the globes and the add wave
    public bool Immobile;               // a set piece (a hub, a bellows, a stump): it turns and casts but never walks
    public float EnrageAt = 240f;       // seconds until the soft enrage
    public float EngageRange = 15f;
    public float Tempo = 1f;            // all cooldowns are divided by this (pinnacle remixes run hotter)

    /// <summary>A copy with its own move list, for the remixes of the region bosses.</summary>
    public BossKit Derive(string id, Action<BossKit> change = null)
    {
        var k = (BossKit)MemberwiseClone();
        k.Id = id;
        k.Basic = Basic?.Clone();
        k.Moves = new BossMoveDef[Moves.Length];
        for (int i = 0; i < Moves.Length; i++) k.Moves[i] = Moves[i].Clone();
        k.Haste = (float[])Haste.Clone(); k.Gcd = (float[])Gcd.Clone();
        change?.Invoke(k);
        return k;
    }
}

internal static class BossKits
{
    private static readonly Dictionary<string, BossKit> All = Build();

    public static BossKit Get(string id) => id != null && All.TryGetValue(id, out var k) ? k : All["*"];
    public static bool Has(string id) => id != null && id != "*" && All.ContainsKey(id);
    /// <summary>Adds (or replaces) a kit: named encounters register theirs under the encounter id.</summary>
    public static void Register(BossKit k) { if (k?.Id != null) All[k.Id] = k; }
    /// <summary>Stands in for a kit while a brain is only a remote control for a script (patrols, set pieces).</summary>
    internal static readonly BossKit None = new() { Id = "-", Short = "", Basic = new BossMoveDef { Id = "none", Kind = MoveK.Cone } };

    private static Dictionary<string, BossKit> Build()
    {
        var all = new Dictionary<string, BossKit>();
        void Add(BossKit k) => all[k.Id] = k;

        // ── 掘墓者・葛魯姆 — a shovel, a graveyard and everything buried in it ──
        Add(new BossKit
        {
            Id = "boss_gravedigger", Short = "葛魯姆", El = Element.Physical,
            PhaseLines = new[] { "「土還沒涼……躺回去。」", "「這座墳，我替你挖得夠深了。」" },
            EnrageLine = "「天黑了。該封土了。」",
            WaveAdds = new[] { "husk", "husk", "skeleton" },
            Basic = new BossMoveDef { Id = "spade_swing", Kind = MoveK.Cone, Radius = 5.6f, Arc = 90f, Wind = 0.7f, Recover = 0.45f, Anim = "Sword_Regular_A", Contact = 0.4f },
            Moves = new[]
            {
                new BossMoveDef { Id = "shovel_sweep", Kind = MoveK.Cone, Cd = 7f, MaxRange = 8f, Weight = 1.2f, Radius = 7.4f, Arc = 150f, Wind = 1f, Recover = 0.8f,
                    Anim = "Sword_Attack", Contact = 0.55f, Dmg = 1.5f, Status = St.Knock(9f) },
                new BossMoveDef { Id = "grave_hands", Kind = MoveK.Eruptions, Cd = 9f, MaxRange = 24f, Count = 2, PerPhase = 1f, Interval = 0.55f, Radius = 2.3f, Wind = 0.95f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Dmg = 0.95f, Status = St.Slow(0.5f, 2f), Sfx = "earth_cast", HitSfx = "earth_impact" },
                new BossMoveDef { Id = "grave_rush", Kind = MoveK.Charge, Cd = 10f, MinRange = 6f, MaxRange = 18f, PerPhase = 0.5f, Length = 15f, Width = 3.2f, Wind = 0.9f, Recover = 0.9f,
                    Anim = "Sword_Dash", Contact = 0.3f, Dmg = 1.4f, Status = St.Knock(8f) },
                new BossMoveDef { Id = "unearth", Kind = MoveK.Summon, Cd = 20f, First = 9f, Count = 3, PerPhase = 1f, Wind = 1f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, Adds = new[] { "husk", "husk", "skeleton" } },
                new BossMoveDef { Id = "headstone_rain", Kind = MoveK.Rain, Phase = 2, Cd = 15f, Duration = 3f, Interval = 0.42f, PerPhase = 1f, Radius = 2.2f, Wind = 1f, Recover = 0.6f,
                    Anim = "Spell_Simple_Shoot", Dmg = 0.9f, Meteor = true, Sfx = "earth_cast", HitSfx = "earth_impact" },
                new BossMoveDef { Id = "tomb_quake", Kind = MoveK.Slam, Phase = 3, Cd = 11f, MaxRange = 12f, Radius = 5f, Wind = 1f, Recover = 0.9f,
                    Anim = "Sword_Attack", Contact = 0.55f, Dmg = 1.6f, Rings = 3, RingWidth = 3f, RingDmg = 1f, HitSfx = "ground_slam" },
            },
        });

        // ── 霜骸領主・赫爾蒙 — cold steel and a mountain's worth of winter ──
        Add(new BossKit
        {
            Id = "boss_rimelord", Short = "赫爾蒙", El = Element.Frost,
            PhaseLines = new[] { "「你的火，暖不了這座山。」", "「跪下。雪會記得你的形狀。」" },
            EnrageLine = "「風雪不等人。」",
            WaveAdds = new[] { "frostbound", "rime_stalker", "frostbound" },
            Basic = new BossMoveDef { Id = "rime_cut", Kind = MoveK.Cone, Radius = 5.8f, Arc = 90f, Wind = 0.7f, Recover = 0.45f, Anim = "Sword_Regular_A", Contact = 0.4f,
                El = Element.Frost, Status = St.Chill(0.2f) },
            Moves = new[]
            {
                new BossMoveDef { Id = "chilling_cleave", Kind = MoveK.Cone, Cd = 7f, MaxRange = 8.5f, Weight = 1.2f, Radius = 7.8f, Arc = 130f, Wind = 0.95f, Recover = 0.8f,
                    Anim = "Sword_Attack", Contact = 0.55f, El = Element.Frost, Dmg = 1.5f, Status = St.Chill(0.5f, 4f) },
                new BossMoveDef { Id = "ice_lances", Kind = MoveK.Fan, Cd = 8f, MinRange = 5f, MaxRange = 24f, Count = 3, PerPhase = 1f, WavesPerPhase = 0.5f, Spread = 16f, Speed = 16f, Reach = 24f, ShotRadius = 0.5f,
                    Wind = 0.8f, Interval = 0.7f, Recover = 0.6f, Anim = "OverhandThrow", Contact = 0.45f, El = Element.Frost, Dmg = 0.9f, Status = St.Chill(0.35f), Sfx = "frost_cast" },
                new BossMoveDef { Id = "hailfall", Kind = MoveK.Rain, Cd = 16f, Duration = 4f, Interval = 0.45f, PerPhase = 0.5f, Radius = 2.3f, Wind = 1.1f, Recover = 0.6f,
                    Anim = "Spell_Simple_Shoot", El = Element.Frost, Dmg = 0.9f, Status = St.Chill(0.6f, 3f), Meteor = true, HitSfx = "frost_shatter" },
                new BossMoveDef { Id = "glacier_leap", Kind = MoveK.Leap, Cd = 10f, MinRange = 6f, MaxRange = 20f, Radius = 4.2f, Wind = 1f, Recover = 0.9f,
                    El = Element.Frost, Dmg = 1.6f, Status = St.Chill(0.4f), HitSfx = "leap_land" },
                new BossMoveDef { Id = "frostbound_call", Kind = MoveK.Summon, Cd = 22f, First = 12f, Count = 2, PerPhase = 1f, Wind = 1f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Frost, Adds = new[] { "frostbound" } },
                new BossMoveDef { Id = "blizzard", Kind = MoveK.Donut, Phase = 2, Cd = 18f, MaxRange = 16f, Inner = 4.6f, Radius = 15f, Wind = 1.5f, Recover = 1f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Frost, Dmg = 1.8f, Status = St.Chill(0.7f, 4f), Warn = "暴雪將至——退到領主身邊！", Sfx = "wind_cast", HitSfx = "nova" },
                new BossMoveDef { Id = "permafrost", Kind = MoveK.Slam, Phase = 3, Cd = 12f, MaxRange = 11f, Radius = 5.2f, Wind = 0.95f, Recover = 0.8f,
                    Anim = "Sword_Attack", Contact = 0.55f, El = Element.Frost, Dmg = 1.5f, Status = St.Chill(0.5f), Rings = 2, RingDmg = 1f, HitSfx = "ground_slam" },
            },
        });

        // ── 腐骨主教・瑪爾薩 — a caster who keeps her distance and lets the dead do the walking ──
        Add(new BossKit
        {
            Id = "boss_bishop", Short = "瑪爾薩", El = Element.Shadow, Ranged = true, KeepMin = 6.5f, KeepMax = 13f,
            PhaseLines = new[] { "「骨頭比信仰誠實。讓我看看你的。」", "「安靜。主正在聆聽你的心跳停下。」" },
            EnrageLine = "「禱詞已盡，只剩埋葬。」",
            WaveAdds = new[] { "skeleton", "skeleton_thrower", "cultist" },
            Basic = new BossMoveDef { Id = "shadow_bolt", Kind = MoveK.Fan, Speed = 15f, Reach = 24f, Wind = 0.6f, Recover = 0.4f, Anim = "Spell_Simple_Shoot", Contact = 0.4f,
                El = Element.Shadow, Dmg = 0.9f },
            Moves = new[]
            {
                new BossMoveDef { Id = "bolt_fan", Kind = MoveK.Fan, Cd = 6f, MinRange = 3f, MaxRange = 22f, Count = 5, PerPhase = 1f, WavesPerPhase = 1f, Spread = 13f, Speed = 12f, Reach = 22f,
                    Wind = 0.8f, Interval = 0.75f, Recover = 0.5f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Dmg = 0.8f },
                new BossMoveDef { Id = "bone_prison", Kind = MoveK.Prison, Cd = 13f, MaxRange = 22f, Count = 8, Length = 5.2f, Radius = 1.6f, Wind = 1f, Interval = 0.7f, Recover = 0.6f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, Dmg = 1.1f, Status = St.Slow(0.3f, 1.5f), Sfx = "earth_cast", HitSfx = "hit_bone" },
                new BossMoveDef { Id = "raise_dead", Kind = MoveK.Summon, Cd = 20f, First = 10f, Count = 3, PerPhase = 1f, Wind = 1f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Adds = new[] { "skeleton", "skeleton", "skeleton_thrower" } },
                new BossMoveDef { Id = "grave_step", Kind = MoveK.Teleport, Cd = 7f, First = 2f, MaxRange = 5.5f, Weight = 3f, Recover = 0.3f, El = Element.Shadow, Away = true },
                new BossMoveDef { Id = "shadow_nova", Kind = MoveK.Slam, Cd = 9f, First = 3f, MaxRange = 6.5f, Weight = 1.5f, Radius = 7f, Wind = 1.1f, Recover = 0.7f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Dmg = 1.6f, Status = St.Knock(10f), Shots = 8, Speed = 8f, Reach = 16f, HitSfx = "nova" },
                new BossMoveDef { Id = "spiral_orbs", Kind = MoveK.Spiral, Phase = 2, Cd = 14f, Duration = 3f, Count = 2, PerPhase = 1f, Interval = 0.16f, Rotate = 14f, Speed = 7f, Reach = 18f,
                    Wind = 0.9f, Recover = 0.6f, El = Element.Shadow, Dmg = 0.6f },
                new BossMoveDef { Id = "ossuary_rain", Kind = MoveK.Rain, Phase = 3, Cd = 15f, Duration = 4f, Interval = 0.4f, Count = 2, Radius = 2.2f, Wind = 1f, Recover = 0.6f,
                    Anim = "Spell_Simple_Shoot", Dmg = 0.9f, Meteor = true, Sfx = "earth_cast", HitSfx = "hit_bone" },
            },
        });

        // ── 沼母・艾絲珂 — she does not chase; the swamp brings you to her ──
        Add(new BossKit
        {
            Id = "boss_mire_mother", Short = "艾絲珂", El = Element.Poison, Ranged = true, KeepMin = 0f, KeepMax = 11f,
            PhaseLines = new[] { "「孩子們餓了……你聞起來好溫暖。」", "「沉下來吧。泥裡沒有痛。」" },
            EnrageLine = "「沼澤要漲潮了。」",
            WaveAdds = new[] { "mire_husk" }, WaveCount = 4,
            Basic = new BossMoveDef { Id = "bile_spit", Kind = MoveK.Fan, Speed = 13f, Reach = 22f, Wind = 0.6f, Recover = 0.4f, Anim = "Spell_Simple_Shoot", Contact = 0.4f,
                El = Element.Poison, Dmg = 0.9f, Status = St.Poison(0.3f, 3f) },
            Moves = new[]
            {
                new BossMoveDef { Id = "root_lash", Kind = MoveK.Cone, Cd = 5f, MaxRange = 6f, Weight = 2f, Radius = 6.5f, Arc = 120f, Wind = 0.8f, Recover = 0.6f,
                    Anim = "Melee_Hook", Contact = 0.35f, El = Element.Poison, Dmg = 1.3f, Status = St.Poison(0.3f, 3f) },
                new BossMoveDef { Id = "mire_rain", Kind = MoveK.Rain, Cd = 14f, Duration = 3.5f, Interval = 0.5f, PerPhase = 0.5f, Radius = 2.4f, Wind = 1f, Recover = 0.6f,
                    Anim = "Spell_Simple_Shoot", El = Element.Poison, Dmg = 0.6f, Meteor = true, ZoneDur = 6f, ZoneDmg = 0.16f, ZoneStatus = St.Poison(1.2f, 3f), HitSfx = "poison_splash" },
                new BossMoveDef { Id = "strangleroot", Kind = MoveK.Line, Cd = 9f, MinRange = 3f, MaxRange = 18f, PerPhase = 1f, Spread = 24f, Length = 17f, Width = 2.4f, Wind = 0.9f, Recover = 0.6f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Poison, Dmg = 1f, Status = St.Root(1.6f), Sfx = "earth_cast", HitSfx = "earth_impact" },
                new BossMoveDef { Id = "spore_volley", Kind = MoveK.Fan, Cd = 7f, MinRange = 4f, MaxRange = 22f, Count = 4, PerPhase = 1f, Waves = 2, WavesPerPhase = 0.5f, Spread = 15f, Speed = 9f, Reach = 20f,
                    Wind = 0.75f, Interval = 0.7f, Recover = 0.5f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Poison, Dmg = 0.7f, Status = St.Poison(0.25f, 3f) },
                new BossMoveDef { Id = "brood_call", Kind = MoveK.Summon, Cd = 20f, First = 10f, Count = 3, PerPhase = 1f, Wind = 1f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Poison, Adds = new[] { "mire_husk" } },
                new BossMoveDef { Id = "devouring_pull", Kind = MoveK.PullSlam, Cd = 13f, First = 6f, MinRange = 5f, MaxRange = 15f, Length = 16f, Width = 1.8f, Radius = 4.6f, Wind = 1.25f, Recover = 1f,
                    Anim = "Melee_Hook", Contact = 0.35f, El = Element.Poison, Dmg = 1.9f, Status = St.Poison(0.5f, 4f), HitSfx = "ground_slam" },
                new BossMoveDef { Id = "spore_bloom", Kind = MoveK.Homing, Phase = 2, Cd = 12f, Count = 3, PerPhase = 1f, Speed = 5.2f, Turn = 2.2f, Reach = 26f, ShotRadius = 0.6f, Wind = 0.8f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Poison, Dmg = 0.9f, Status = St.Poison(0.3f, 3f) },
                new BossMoveDef { Id = "bog_surge", Kind = MoveK.Slam, Phase = 3, Cd = 12f, MaxRange = 10f, Radius = 5f, Wind = 1f, Recover = 0.8f,
                    Anim = "Melee_Hook", Contact = 0.35f, El = Element.Poison, Dmg = 1.5f, Rings = 3, RingDmg = 0.9f, HitSfx = "ground_slam" },
            },
        });

        // ── 鏽砂暴君・伊薩克 — a king of brass who still rules by the glaive ──
        Add(new BossKit
        {
            Id = "boss_sand_tyrant", Short = "伊薩克", El = Element.Lightning,
            PhaseLines = new[] { "「王座鏽了，王還沒有。」", "「連風沙都得向我低頭！」" },
            EnrageLine = "「黃銅的耐心到此為止。」",
            WaveAdds = new[] { "sand_revenant", "sand_husk", "sand_revenant" },
            Basic = new BossMoveDef { Id = "glaive_cut", Kind = MoveK.Cone, Radius = 6.4f, Arc = 90f, Wind = 0.7f, Recover = 0.45f, Anim = "Sword_Regular_C", Contact = 0.4f },
            Moves = new[]
            {
                new BossMoveDef { Id = "glaive_sweep", Kind = MoveK.Cone, Cd = 7f, MaxRange = 9f, Weight = 1.2f, Radius = 8.2f, Arc = 180f, Wind = 1f, Recover = 0.8f,
                    Anim = "Sword_Attack", Contact = 0.55f, Dmg = 1.6f, Status = St.Knock(8f) },
                new BossMoveDef { Id = "tyrant_charge", Kind = MoveK.Charge, Cd = 10f, MinRange = 6f, MaxRange = 19f, PerPhase = 0.5f, Length = 16f, Width = 3.4f, Wind = 0.85f, Recover = 0.9f,
                    Anim = "Sword_Dash", Contact = 0.3f, El = Element.Lightning, Dmg = 1.5f },
                new BossMoveDef { Id = "storm_cross", Kind = MoveK.Cross, Cd = 12f, MaxRange = 18f, Count = 2, PerPhase = 0.5f, WavesPerPhase = 1.5f, Rotate = 30f, Length = 17f, Width = 2.2f,
                    Wind = 1f, Interval = 0.75f, Recover = 0.7f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Lightning, Dmg = 1.3f, Sfx = "lightning_cast", HitSfx = "thunder" },
                new BossMoveDef { Id = "shock_rings", Kind = MoveK.Slam, Cd = 12f, MaxRange = 12f, Radius = 4.5f, Wind = 0.9f, Recover = 0.8f,
                    Anim = "Sword_Attack", Contact = 0.55f, El = Element.Lightning, Dmg = 1.4f, Rings = 2, RingsPerPhase = 1f, RingDmg = 1f, HitSfx = "ground_slam" },
                new BossMoveDef { Id = "brass_muster", Kind = MoveK.Summon, Phase = 2, Cd = 26f, Count = 1, PerPhase = 1f, Wind = 1f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Lightning, Adds = new[] { "brass_sentinel" } },
                new BossMoveDef { Id = "sandstorm_bolts", Kind = MoveK.Rain, Phase = 3, Cd = 13f, Duration = 4f, Interval = 0.33f, Count = 2, Radius = 2f, Wind = 0.9f, Recover = 0.6f,
                    Anim = "Spell_Simple_Shoot", El = Element.Lightning, Dmg = 1f, Meteor = true, Sfx = "lightning_cast", HitSfx = "storm_bolt" },
            },
        });

        // ── 寂滅大導師・賽倫 — bells, silence and the choir that keeps both ──
        Add(new BossKit
        {
            Id = "boss_grand_hush", Short = "賽倫", El = Element.Shadow, Ranged = true, KeepMin = 6f, KeepMax = 13f,
            PhaseLines = new[] { "「聽，鐘聲在為你而靜。」", "「最後一盞燈，由我親手捻熄。」" },
            EnrageLine = "「靜默，不容再拖延。」",
            WaveAdds = new[] { "cultist", "cult_caster", "cultist" },
            Basic = new BossMoveDef { Id = "hush_bolt", Kind = MoveK.Fan, Speed = 15f, Reach = 24f, Wind = 0.6f, Recover = 0.4f, Anim = "Spell_Simple_Shoot", Contact = 0.4f,
                El = Element.Shadow, Dmg = 0.95f },
            Moves = new[]
            {
                new BossMoveDef { Id = "bell_toll", Kind = MoveK.Slam, Cd = 11f, MaxRange = 16f, Radius = 3.6f, Wind = 0.9f, Recover = 0.6f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Dmg = 1.2f, Rings = 3, RingsPerPhase = 1f, RingWidth = 3.2f, RingDmg = 1.1f, Sfx = "bell_toll", HitSfx = "bell_toll" },
                new BossMoveDef { Id = "seeking_orbs", Kind = MoveK.Homing, Cd = 12f, Count = 3, PerPhase = 1f, Speed = 5.5f, Turn = 2.2f, Reach = 28f, ShotRadius = 0.6f, Wind = 0.8f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Dmg = 0.95f },
                new BossMoveDef { Id = "silent_beam", Kind = MoveK.Line, Cd = 8f, MinRange = 3f, MaxRange = 22f, PerPhase = 1f, WavesPerPhase = 0.5f, Spread = 20f, Length = 22f, Width = 2f,
                    Wind = 0.9f, Interval = 0.75f, Recover = 0.6f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Dmg = 1.3f, HitSfx = "shadow_impact" },
                new BossMoveDef { Id = "hush_step", Kind = MoveK.Teleport, Cd = 6.5f, First = 2f, MaxRange = 6f, Weight = 3f, Recover = 0.3f, El = Element.Shadow, Away = true },
                new BossMoveDef { Id = "silence_field", Kind = MoveK.Eruptions, Phase = 2, Cd = 15f, MaxRange = 24f, Radius = 5f, Wind = 1.1f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Dmg = 0.4f, Status = St.Slow(0.6f, 1.5f), ZoneDur = 7f, ZoneDmg = 0.1f, ZoneStatus = St.Slow(0.6f, 1.2f),
                    Sfx = "hex_cast", HitSfx = "hex_impact" },
                new BossMoveDef { Id = "choir_call", Kind = MoveK.Summon, Phase = 2, Cd = 24f, Count = 2, PerPhase = 1f, Wind = 1f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Adds = new[] { "choir", "cultist" } },
                new BossMoveDef { Id = "requiem", Kind = MoveK.Spiral, Phase = 3, Cd = 16f, Duration = 3.5f, Count = 3, Interval = 0.17f, Rotate = 13f, Speed = 7f, Reach = 18f,
                    Wind = 0.9f, Recover = 0.6f, El = Element.Shadow, Dmg = 0.65f },
            },
        });

        // ── 寂主・奧薩洛斯 — the Lord of Silence; by phase three he does not stop ──
        Add(new BossKit
        {
            Id = "boss_osaros", Short = "奧薩洛斯", El = Element.Shadow,
            PhaseLines = new[] { "「千年了……還有人捨不得這點餘燼。」", "「看著我。這就是火熄之後的世界。」" },
            EnrageLine = "「夠了。讓一切歸於無聲。」",
            WaveAdds = null, WaveCount = 4,
            Haste = new[] { 1f, 1.15f, 1.4f }, Gcd = new[] { 1.4f, 1f, 0.35f },
            Basic = new BossMoveDef { Id = "void_cut", Kind = MoveK.Cone, Radius = 6.6f, Arc = 90f, Wind = 0.65f, Recover = 0.4f, Anim = "Sword_Regular_A", Contact = 0.4f, El = Element.Shadow },
            Moves = new[]
            {
                new BossMoveDef { Id = "ashen_combo", Kind = MoveK.Cone, Cd = 6f, MaxRange = 9f, Weight = 1.3f, Count = 3, PerPhase = 0.5f, Radius = 7.8f, Arc = 110f, Wind = 0.8f, Interval = 0.65f, Recover = 0.7f,
                    Anims = new[] { "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C", "Sword_Attack" }, Contact = 0.4f, El = Element.Shadow, Dmg = 1.15f },
                new BossMoveDef { Id = "crossing_slashes", Kind = MoveK.Cross, Cd = 9f, MaxRange = 22f, Count = 2, WavesPerPhase = 1f, Rotate = 45f, Length = 10f, Width = 2.4f, AtHero = true,
                    Wind = 0.9f, Interval = 0.7f, Recover = 0.6f, Anims = new[] { "Sword_Regular_B", "Sword_Regular_C" }, Contact = 0.4f, El = Element.Shadow, Dmg = 1.4f, Sfx = "swing_heavy", HitSfx = "shadow_impact" },
                new BossMoveDef { Id = "void_rain", Kind = MoveK.Rain, Cd = 16f, Duration = 4f, Interval = 0.38f, PerPhase = 0.5f, Radius = 2.6f, Wind = 1f, Recover = 0.6f,
                    Anim = "Spell_Simple_Shoot", El = Element.Shadow, Dmg = 1.1f, Meteor = true, Sfx = "meteor_fall", HitSfx = "meteor_explode" },
                new BossMoveDef { Id = "rift_strike", Kind = MoveK.TeleStrike, Cd = 9f, MinRange = 7f, Radius = 4.2f, Wind = 0.9f, Recover = 0.7f,
                    Anim = "Sword_Attack", El = Element.Shadow, Dmg = 1.7f, HitSfx = "ground_slam" },
                new BossMoveDef { Id = "void_spiral", Kind = MoveK.Spiral, Phase = 2, Cd = 15f, Duration = 3.5f, Count = 3, Interval = 0.18f, Rotate = 12f, Speed = 7.5f, Reach = 18f,
                    Wind = 0.9f, Recover = 0.6f, El = Element.Shadow, Dmg = 0.7f },
                new BossMoveDef { Id = "extinguish", Kind = MoveK.Donut, Phase = 2, Cd = 28f, First = 5f, MaxRange = 15f, Weight = 1.5f, Inner = 5.5f, Radius = 0f, Wind = 3f, Recover = 1.2f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Dmg = 2.6f, Warn = "熄滅將臨——貼近寂主！", Sfx = "boss_roar", HitSfx = "nova" },
                new BossMoveDef { Id = "fallen_knights", Kind = MoveK.Summon, Phase = 2, Cd = 30f, First = 8f, Count = 1, PerPhase = 1f, Wind = 1f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Shadow, Adds = new[] { "knight" } },
                new BossMoveDef { Id = "umbral_quake", Kind = MoveK.Slam, Phase = 3, Cd = 10f, MaxRange = 12f, Radius = 5.2f, Wind = 0.9f, Recover = 0.7f,
                    Anim = "Sword_Attack", Contact = 0.55f, El = Element.Shadow, Dmg = 1.6f, Rings = 3, RingDmg = 1.1f, HitSfx = "ground_slam" },
            },
        });

        // ── any other monster flagged as a boss gets a plain but complete fight ──
        Add(new BossKit
        {
            Id = "*", Short = "燼淵之主", El = Element.Fire,
            PhaseLines = new[] { "「餘燼……還不肯熄。」", "「那就一起燒盡。」" },
            EnrageLine = "「灰，終將覆蓋一切。」",
            Basic = new BossMoveDef { Id = "swing", Kind = MoveK.Cone, Radius = 5.5f, Arc = 90f, Wind = 0.7f, Recover = 0.45f, Anim = "Sword_Regular_A", Contact = 0.4f },
            Moves = new[]
            {
                new BossMoveDef { Id = "slam", Kind = MoveK.Slam, Cd = 9f, MaxRange = 9f, Radius = 5f, Wind = 1f, Recover = 0.8f, Contact = 0.55f, El = Element.Fire, Dmg = 1.5f, RingsPerPhase = 1f, HitSfx = "ground_slam" },
                new BossMoveDef { Id = "eruptions", Kind = MoveK.Eruptions, Cd = 10f, MaxRange = 24f, Count = 2, PerPhase = 1f, Radius = 2.4f, Wind = 0.95f, Recover = 0.5f,
                    Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Fire, Dmg = 0.9f, Status = St.Burn(0.4f, 3f) },
                new BossMoveDef { Id = "rush", Kind = MoveK.Charge, Cd = 11f, MinRange = 6f, MaxRange = 18f, Length = 14f, Width = 3f, Wind = 0.9f, Recover = 0.9f, Anim = "Sword_Dash", Contact = 0.3f, Dmg = 1.4f },
                new BossMoveDef { Id = "call", Kind = MoveK.Summon, Phase = 2, Cd = 22f, Count = 2, PerPhase = 1f, Wind = 1f, Recover = 0.5f, Anim = "Spell_Simple_Shoot", Contact = 0.4f, El = Element.Fire },
            },
        });
        return all;
    }
}

/// <summary>
/// Runs a region boss. A boss picks among its unlocked moves by cooldown, range and line of sight, otherwise closes
/// in (or keeps its distance, for casters) and uses a telegraphed basic attack. At 66 % and 33 % life it roars
/// (briefly untouchable, a knock-back nova, two health globes, an add wave), speeds up and unlocks new moves; after
/// four minutes it turns to a soft enrage. Every damaging step is shown on the ground for at least 0.6 s.
/// Timed steps run on <see cref="HazardClock"/>: steps of the move in progress are dropped when the boss is staggered,
/// hazards already marked on the ground still resolve, and everything is cleared when the boss dies or resets.
/// </summary>
public sealed class BossBrain
{
    public int Phase = 1;
    public bool Engaged => _engaged;
    public bool Enraged => _enraged;
    public float FightTime => _fightT;
    public string CurrentMove => _cur?.Id;

    private const float RoarTime = 1.25f, KiteMax = 1.6f;

    // ── seam for named encounters (Named*.cs) ──
    /// <summary>Called when a move starts / a phase begins / the fight starts / the fight resets without a kill.</summary>
    internal Action<BossMoveDef> MoveStarted;
    internal Action<int> PhaseBegan;
    internal Action Engaging, Disengaged;
    /// <summary>Called when a marked circle of a move goes off: (move, centre, radius).</summary>
    internal Action<BossMoveDef, Vector2, float> Dropped;
    /// <summary>While this returns true the brain neither moves nor picks an attack: a script is acting through the monster.</summary>
    internal Func<bool> Held;
    /// <summary>Moves the script wants refused right now (return true to veto).</summary>
    internal Func<BossMoveDef, bool> Veto;
    internal Vector2 Arena => _arena;
    internal float ArenaR => _arenaR;
    internal Vector2 AimPoint => _aim;
    internal bool Acting => _lock > 0f || _roaring;
    internal float Damage { get => _dmg; set => _dmg = value; }
    internal bool Scripted => _script != null;
    internal void SetArena(Vector2 centre, float radius) { _arena = centre; _arenaR = radius; _arenaFixed = true; }
    /// <summary>Runs a move as soon as the one in progress ends, whatever its cooldown or range.</summary>
    internal void Force(BossMoveDef d) { if (d != null) _forced.Enqueue(d); }
    /// <summary>Drops the move in progress and keeps the boss from acting for a while (a script takes over).</summary>
    internal void Suspend(float seconds) { Log.Guard("boss", CancelMove); _lock = Mathf.Max(_lock, seconds); _gcd = Mathf.Max(_gcd, 0.4f); }
    /// <summary>An add spawned by a script: it is cleared with the boss's own when the fight ends or resets.</summary>
    internal void Adopt(Monster add) { if (add != null) _adds.Add(add); }
    internal int AddCount { get { PruneAdds(); return _adds.Count; } }

    private readonly Queue<BossMoveDef> _forced = new();
    private readonly Action<float> _script;
    private bool _arenaFixed;

    /// <summary>A brain that only runs <paramref name="tick"/>: the monster's own state machine is switched off while it is set.</summary>
    internal static BossBrain Remote(Monster m, Action<float> tick) => new(m, tick ?? (_ => { }));

    private BossBrain(Monster m, Action<float> script)
    {
        _m = m; _script = script; _kit = BossKits.None;
        _cd = Array.Empty<float>(); _w = Array.Empty<float>();
        _baseSpeed = m.SpeedMul;
        _home = _arena = _aim = m.Pos2;
        _chargeStep = ChargeStep; _leapStep = LeapStep;
    }

    private readonly Monster _m;
    private readonly BossKit _kit;
    private readonly float[] _cd, _w;
    private readonly float _baseSpeed;
    private readonly object _moveTok = new();
    private readonly List<Node3D> _moveTeles = new(), _hazTeles = new();
    private readonly List<Monster> _adds = new();
    private readonly List<GroundZone> _zones = new();
    private readonly List<Projectile> _shots = new();
    private readonly HashSet<Actor> _struck = new();
    private readonly Action<float> _chargeStep, _leapStep;
    private static readonly List<Actor> Tmp = new();
    private static readonly Predicate<Node3D> GoneNode = n => n == null || !GodotObject.IsInstanceValid(n);
    private static readonly Predicate<Monster> GoneAdd = a => a == null || !GodotObject.IsInstanceValid(a) || a.Dead;
    private static readonly Predicate<GroundZone> GoneZone = z => z == null || !GodotObject.IsInstanceValid(z) || z.Done;
    private static readonly Predicate<Projectile> GoneShot = s => s == null || !GodotObject.IsInstanceValid(s);

    private bool _engaged, _everEngaged, _returning, _hurt, _enraged, _roaring, _vanished, _kiteSpent;
    private float _fightT, _deadT, _leashT, _gcd, _basicCd, _lock, _roarT, _roarDr = 1f, _rampT, _kite = KiteMax;
    private float _haste = 1f, _windHaste = 1f, _dmg = 1f, _arenaR = 13f, _faultT;
    private Vector2 _home, _arena, _aim;
    private BossMoveDef _cur, _last;
    private Action<float> _motion;
    private Vector2 _mFrom, _mTo;
    private float _mT, _mDur;
    private BossMoveDef _mDef;

    public BossBrain(Monster m)
    {
        _m = m;
        _kit = BossKits.Get(m.Def?.Id);
        _cd = new float[_kit.Moves.Length];
        _w = new float[_kit.Moves.Length];
        _baseSpeed = m.SpeedMul;
        _home = _arena = _aim = m.Pos2;
        _chargeStep = ChargeStep;
        _leapStep = LeapStep;
        ResetCooldowns();
        if (_kit.Id == "*") Log.Warn("boss", $"no move list for '{m.Def?.Id}': using the generic one");
        Log.Info("boss", $"{m.Def?.Id} ready: moves={_kit.Moves.Length} level={m.Level} life={m.MaxLife:F0} dmg={m.BaseDamage:F0}");
        Log.Guard("named", () => NamedHub.BrainCreated(m, this));      // a named encounter re-attaches its script to a fresh brain
    }

    // ───────────────────────── contract ─────────────────────────

    /// <summary>Called by Monster._Process every unpaused frame while the boss is alive and not staggered.</summary>
    public void Tick(float dt)
    {
        if (_faultT > 0f) { _faultT -= dt; return; }       // after a failure the boss stands still for a moment instead of failing every frame
        if (_script != null)
        {
            try { _script(dt); }
            catch (Exception e) { Log.Error("boss", $"{_m.Def?.Id} remote script failed", e); _faultT = 1.5f; }
            return;
        }
        try { Step(dt); }
        catch (Exception e)
        {
            Log.Error("boss", $"{_m.Def?.Id} brain failed (move={_cur?.Id ?? "none"} phase={Phase})", e);
            Log.Guard("boss", CancelMove);
            _faultT = 1.5f;
        }
    }

    /// <summary>Called by Monster.TakeHit after every hit the boss survives.</summary>
    public void OnDamaged() => _hurt = true;

    /// <summary>Called by Monster when the stagger meter fills: the move in progress is cancelled.</summary>
    public void Interrupt()
    {
        if (_script != null) return;
        Log.Guard("boss", () =>
        {
            if (Log.Verbose) Log.Debug("boss", $"{_m.Def?.Id} staggered: move {_cur?.Id ?? "none"} cancelled");
            if (_roaring) EndRoar();
            CancelMove();
            _gcd = 1.2f;
            _basicCd = Mathf.Max(_basicCd, 0.8f);
        });
    }

    /// <summary>Called by Monster.Die: nothing the boss started may outlive it.</summary>
    public void OnDeath()
    {
        if (_script != null) return;
        Log.Guard("boss", () =>
        {
            if (_roaring) { _roaring = false; _roarT = 0f; _m.DrMul = _roarDr; }
            CancelMove();
            ClearHazards();
            KillAdds();
            _m.Visible = true;
            _engaged = false;
            if (_everEngaged) Fx.Shake(0.5f, 0.6f);
            Log.Info("boss", $"{_m.Def?.Id} slain: phase={Phase} fight={_fightT:F0}s enraged={_enraged} tier={Game.I?.WorldTier ?? 0}");
        });
    }

    // ───────────────────────── frame ─────────────────────────

    private void Step(float dt)
    {
        var p = World.P;
        if (p == null || World.Root == null) return;
        if (!_engaged) { Dormant(dt, p); return; }

        _fightT += dt;
        _hurt = false;
        var taunt = _m.TauntBy;
        if (taunt != null && GodotObject.IsInstanceValid(taunt) && !taunt.Dead) _aim = taunt.Pos2;
        else if (!p.Dead && !p.Stealthed) _aim = p.Pos2;      // a hidden hero is hunted where last seen

        if (_roaring)
        {
            _roarT -= dt;
            if (_roarT <= 0f) EndRoar();
            return;
        }

        if (p.Dead)
        {
            if (_deadT <= 0f) CancelMove();
            _deadT += dt;
            _m.BossIdle();
            if (_deadT >= 2.5f) Disengage("the hero fell");
            return;
        }
        _deadT = 0f;

        if (_aim.DistanceTo(_arena) > _arenaR + 30f)
        {
            _leashT += dt;
            if (_leashT > 6f) { Disengage("the hero left"); return; }
        }
        else _leashT = 0f;

        if (!_enraged) { if (_fightT >= _kit.EnrageAt) BeginEnrage(); }
        else
        {
            _rampT += dt;
            if (_rampT >= 15f) { _rampT = 0f; _dmg = Mathf.Min(_dmg * 1.08f, 3f); }
        }

        float frac = _m.Life / Mathf.Max(1f, _m.MaxLife);
        int want = frac <= 0.33f ? 3 : frac <= 0.66f ? 2 : 1;
        if (want > Phase) { BeginPhase(Phase + 1); return; }

        for (int i = 0; i < _cd.Length; i++) if (_cd[i] > 0f) _cd[i] -= dt;
        if (_basicCd > 0f) _basicCd -= dt;

        if (_lock > 0f)
        {
            _lock -= dt;
            _motion?.Invoke(dt);
            if (_lock > 0f) return;
            bool special = _cur != null && !ReferenceEquals(_cur, _kit.Basic);
            _motion = null; _cur = null;
            Ground();
            if (special) _gcd = _kit.Gcd[Mathf.Clamp(Phase - 1, 0, 2)] / (_enraged ? 1.3f : 1f);
        }
        if (_gcd > 0f) _gcd -= dt;
        if (Held != null && Held()) return;               // a script is acting through the monster
        Decide(dt);
    }

    private void Dormant(float dt, Player p)
    {
        bool hurt = _hurt;
        _hurt = false;
        if (!p.Dead)
        {
            Vector2 pos = _m.Pos2, pp = p.Pos2;
            bool seen = !p.Stealthed && pos.DistanceTo(pp) <= _kit.EngageRange && (World.Grid == null || World.Grid.LineShot(pos, pp));
            if (seen || hurt || (!_everEngaged && _m.Aggro)) { Engage(p); return; }
        }
        if (_returning)
        {
            if (_m.Pos2.DistanceTo(_home) > 1.2f) { _m.BossMove(_home, dt, 0.6f); return; }
            _returning = false;
        }
        _m.BossIdle();
    }

    private void Engage(Player p)
    {
        _engaged = true; _everEngaged = true; _returning = false;
        _fightT = 0f; _deadT = 0f; _leashT = 0f; _rampT = 0f;
        _aim = p.Pos2;
        var poi = Game.I?.Level?.Data?.Find("boss");
        if (_arenaFixed) { }
        else if (poi != null && poi.Pos.DistanceTo(_home) < 40f) { _arena = poi.Pos; _arenaR = Mathf.Clamp(poi.Radius, 9f, 24f); }
        else { _arena = _home; _arenaR = 13f; }
        Pace();
        _m.WakePack();
        _m.FaceDir(_aim - _m.Pos2);
        _m.Model?.Play("Spell_Simple_Enter", 0.8f, 0.15f, true);
        _lock = 1f; _gcd = 0.8f; _basicCd = 0.6f;
        Snd.At("boss_roar", _m.GlobalPosition);
        Fx.Shake(0.35f, 0.5f);
        // boss music and the pulled-back camera are Game's business: it watches for an awake monster with a Brain
        Log.Info("boss", $"{_m.Def?.Id} engaged: arena=({_arena.X:F0},{_arena.Y:F0}) r={_arenaR:F0} level={_m.Level} tier={Game.I?.WorldTier ?? 0}");
        Engaging?.Invoke();
    }

    /// <summary>The fight is over without a kill: everything is swept away and the boss walks home at full life.</summary>
    private void Disengage(string why)
    {
        if (_roaring) EndRoar();
        CancelMove();
        ClearHazards();
        KillAdds();
        bool wasEnraged = _enraged;
        _engaged = false; _returning = true; _hurt = false; _enraged = false;
        _deadT = 0f; _leashT = 0f; _dmg = 1f;
        Phase = 1;
        Pace();
        ResetCooldowns();
        _m.Life = _m.MaxLife; _m.Barrier = 0f; _m.BarrierLeft = 0f; _m.Stagger = 0f;
        _m.Dots.Clear();
        if (wasEnraged)
            _m.Model?.SetRim(_m.Affixes.Count > 0 ? MonsterDb.Affix(_m.Affixes[0])?.Color ?? Palette.Danger : Colors.Black);
        Log.Info("boss", $"{_m.Def?.Id} disengaged ({why}) after {_fightT:F0}s: back to full life, phase 1");
        _forced.Clear();
        Disengaged?.Invoke();
    }

    private void ResetCooldowns()
    {
        var moves = _kit.Moves;
        for (int i = 0; i < moves.Length; i++)
            _cd[i] = moves[i].First >= 0f ? moves[i].First : moves[i].Cd * Rng.Range(0.15f, 0.55f);
        _gcd = 1.2f; _basicCd = 0.6f;
        _last = null;
    }

    /// <summary>Recomputes tempo from phase and enrage: cooldowns, telegraph speed (capped) and walking speed.</summary>
    private void Pace()
    {
        _haste = _kit.Haste[Mathf.Clamp(Phase - 1, 0, 2)] * (_enraged ? 1.25f : 1f);
        _windHaste = 1f + (_haste - 1f) * 0.6f;
        _m.SpeedMul = _baseSpeed * (1f + (_haste - 1f) * 0.9f);
    }

    // ───────────────────────── phases ─────────────────────────

    private void BeginPhase(int n)
    {
        if (_kit.NoRoar)
        {
            // a lesser boss changes gear without the show: faster, new moves, no pause in the fight
            int was = Phase;
            Phase = n;
            Pace();
            Unlock(n);
            Log.Info("boss", $"{_m.Def?.Id} phase {was}→{n} (quiet) at {_m.Life / Mathf.Max(1f, _m.MaxLife):P0} life, {_fightT:F0}s in");
            PhaseBegan?.Invoke(n);
            return;
        }
        CancelMove();
        int prev = Phase;
        Phase = n;
        Pace();

        _roaring = true; _roarT = RoarTime;
        _roarDr = _m.DrMul;
        _m.DrMul = 0f;                                  // nothing gets through while it roars, not even damage over time
        _m.Untargetable = true;
        _m.FaceDir(_aim - _m.Pos2);
        _m.Model?.Play("Spell_Simple_Enter", 0.7f, 0.1f, true);

        Vector3 feet = _m.GlobalPosition;
        Snd.At("boss_phase", feet);
        Fx.Shake(0.6f, 0.7f);
        Fx.Burst(_m.Chest, _kit.El, 8f, 1.6f);
        Fx.Ring(feet + Vector3.Up * 0.2f, 9f, Palette.Element(_kit.El), 0.7f, _kit.El);
        Fx.BuffAura(_m, _kit.El, RoarTime, true);

        // the nova only shoves: it makes room, it does not hurt
        var p = World.P;
        if (p != null && !p.Dead && p.Pos2.DistanceTo(_m.Pos2) <= 9f) p.ApplyStatus(St.Knock(15f), _m);
        for (int i = 0; i < World.Minions.Count; i++)
        {
            var mi = World.Minions[i];
            if (mi != null && !mi.Dead && mi.Pos2.DistanceTo(_m.Pos2) <= 9f) mi.ApplyStatus(St.Knock(12f), _m);
        }

        string line = n - 2 >= 0 && n - 2 < _kit.PhaseLines.Length ? _kit.PhaseLines[n - 2] : null;
        if (line != null) Game.I?.Hud?.Toast($"{_kit.Short}：{line}", Palette.Danger);

        var grid = World.Grid;
        for (int k = 0; k < 2; k++)
        {
            Vector2 at = grid != null ? grid.RandomWalkNear(_m.Pos2, 3f, 5.5f) : _m.Pos2 + Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau)) * 4f;
            Loot.DropGlobe(at);
        }

        PruneAdds();
        int wave = Math.Min(_kit.WaveCount + (n - 2), AddCap + 2 - _adds.Count);
        if (wave > 0)
        {
            Snd.At("summon_raise", feet, 0.8f);
            SpawnAdds(_kit.WaveAdds, wave, 1f);
        }

        Unlock(n);
        _gcd = 0.5f;
        Log.Info("boss", $"{_m.Def?.Id} phase {prev}→{n} at {_m.Life / Mathf.Max(1f, _m.MaxLife):P0} life, {_fightT:F0}s in, haste={_haste:0.00} adds={_adds.Count}+{Math.Max(wave, 0)}");
        PhaseBegan?.Invoke(n);
    }

    /// <summary>What a phase unlocks shows up early, one move after the other.</summary>
    private void Unlock(int n)
    {
        var moves = _kit.Moves;
        float soon = 1.2f;
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i].Phase != n) continue;
            if (moves[i].First >= 0f) _cd[i] = moves[i].First;
            else { _cd[i] = soon; soon += 3.5f; }
            if (Log.Verbose) Log.Debug("boss", $"{_m.Def?.Id} phase {n} unlocks {moves[i].Id} ({moves[i].Kind}) in {_cd[i]:0.0}s");
        }
    }

    private void EndRoar()
    {
        _roarT = 0f;
        if (!_roaring) return;
        _roaring = false;
        _m.DrMul = _roarDr;
        _m.Untargetable = _vanished || _m.Dead;
    }

    private void BeginEnrage()
    {
        _enraged = true; _rampT = 0f;
        _dmg *= 1.25f;
        Pace();
        _m.Model?.SetRim(new Color(1f, 0.15f, 0.1f) * 1.8f);
        Snd.At("boss_roar", _m.GlobalPosition);
        Fx.Shake(0.45f, 0.6f);
        Fx.BuffAura(_m, Element.Fire, 4f, true);
        if (!string.IsNullOrEmpty(_kit.EnrageLine)) Game.I?.Hud?.Toast($"{_kit.Short}：{_kit.EnrageLine}", Palette.Danger);
        Log.Info("boss", $"{_m.Def?.Id} soft enrage after {_fightT:F0}s (phase {Phase})");
    }

    // ───────────────────────── choosing ─────────────────────────

    private int AddCap => Phase <= 1 ? 6 : 8;

    private void Decide(float dt)
    {
        Vector2 pos = _m.Pos2, to = _aim - pos;
        float dist = to.Length();
        var grid = World.Grid;
        bool los = grid == null || grid.LineShot(pos, _aim);

        if (_forced.Count > 0) { Start(_forced.Dequeue(), -1, dist); return; }      // a script asked for this move

        if (_gcd <= 0f)
        {
            int pick = Pick(dist, los);
            if (pick >= 0) { Start(_kit.Moves[pick], pick, dist); return; }
            _gcd = 0.25f;                               // nothing fits right now: look again shortly
        }

        if (_kit.Immobile)
        {
            // a set piece: it turns toward the hero and strikes whoever comes within reach, but never leaves its spot
            _m.FaceDir(to, 1f - Mathf.Exp(-4f * dt));
            bool inReach = los && (_kit.Ranged || dist <= _kit.Basic.Radius * 0.85f);
            if (_basicCd <= 0f && inReach && _kit.Basic.Dmg > 0f) Start(_kit.Basic, -1, dist);
            else _m.BossIdle();
            return;
        }

        if (_kit.Ranged)
        {
            if (!los || dist > _kit.KeepMax) { Refill(dt); _m.BossMove(_aim, dt, 0f); return; }
            if (dist < _kit.KeepMin && !_kiteSpent && dist > 0.05f)
            {
                // give ground for a moment, then stand and fight; when cornered the teleport takes over
                Vector2 away = pos - to / dist * 4f;
                bool free = (grid == null || (grid.WalkAt(away) && grid.LineWalk(pos, away))) && away.DistanceTo(_arena) < _arenaR - 1f;
                if (free)
                {
                    _kite -= dt;
                    if (_kite <= 0f) _kiteSpent = true;
                    _m.BossMove(away, dt, 0f);
                    return;
                }
                _kiteSpent = true;
            }
            else Refill(dt);
        }
        else
        {
            float reach = _kit.Basic.Radius * 0.72f;
            if (dist > reach || !los) { _m.BossMove(_aim, dt, 0f); return; }
        }

        _m.FaceDir(to, 1f - Mathf.Exp(-9f * dt));
        if (_basicCd <= 0f) Start(_kit.Basic, -1, dist);
        else _m.BossIdle();
    }

    /// <summary>A caster may back off for KiteMax seconds, then has to hold its ground until the budget is full again.</summary>
    private void Refill(float dt)
    {
        if (_kite < KiteMax) _kite = Mathf.Min(KiteMax, _kite + dt * 0.4f);
        if (_kite >= KiteMax) _kiteSpent = false;
    }

    private int Pick(float dist, bool los)
    {
        var moves = _kit.Moves;
        float total = 0f;
        for (int i = 0; i < moves.Length; i++)
        {
            float w = Usable(moves[i], i, dist, los) ? moves[i].Weight * (ReferenceEquals(moves[i], _last) ? 0.25f : 1f) : 0f;
            _w[i] = w;
            total += w;
        }
        if (total <= 0f) return -1;
        float r = Rng.F() * total;
        int pick = -1;
        for (int i = 0; i < moves.Length; i++)
        {
            if (_w[i] <= 0f) continue;
            pick = i;
            r -= _w[i];
            if (r <= 0f) break;
        }
        return pick;
    }

    private bool Usable(BossMoveDef d, int i, float dist, bool los)
    {
        if (Phase < d.Phase || _cd[i] > 0f || dist < d.MinRange || dist > d.MaxRange) return false;
        if (Veto != null && Veto(d)) return false;
        switch (d.Kind)
        {
            case MoveK.Summon:
                PruneAdds();
                return _adds.Count < AddCap;
            case MoveK.Cone: case MoveK.Line: case MoveK.Fan: case MoveK.PullSlam:
                return los;                             // these need a clear lane to the hero
            case MoveK.Charge:
                return los && (World.Grid == null || World.Grid.LineWalk(_m.Pos2, _aim));
            default:
                return true;
        }
    }

    private void Start(BossMoveDef d, int idx, float dist)
    {
        _cur = d;
        if (idx >= 0)
        {
            _last = d;
            _cd[idx] = d.Cd / (_haste * Mathf.Max(0.2f, _kit.Tempo)) * Rng.Range(0.9f, 1.15f);
            _basicCd = Mathf.Max(_basicCd, 0.5f);
        }
        else _basicCd = _m.Def.Cooldown / _haste;
        Prune();
        if (Log.Verbose) Log.Debug("boss", $"{_m.Def?.Id} move={d.Id} ({d.Kind}) phase={Phase} dist={dist:F1} life={_m.Life / Mathf.Max(1f, _m.MaxLife):P0}");
        _lock = 0f;
        _motion = null;
        switch (d.Kind)
        {
            case MoveK.Cone: DoCone(d); break;
            case MoveK.Slam: DoSlam(d); break;
            case MoveK.Charge: DoCharge(d); break;
            case MoveK.Line: DoLine(d); break;
            case MoveK.Eruptions: DoEruptions(d); break;
            case MoveK.Donut: DoDonut(d); break;
            case MoveK.Fan: DoFan(d); break;
            case MoveK.Spiral: DoSpiral(d); break;
            case MoveK.Homing: DoHoming(d); break;
            case MoveK.Cross: DoCross(d); break;
            case MoveK.Rain: DoRain(d); break;
            case MoveK.Summon: DoSummon(d); break;
            case MoveK.Teleport: DoTeleport(d); break;
            case MoveK.TeleStrike: DoTeleStrike(d); break;
            case MoveK.PullSlam: DoPullSlam(d); break;
            case MoveK.Leap: DoLeap(d); break;
            case MoveK.Prison: DoPrison(d); break;
        }
        MoveStarted?.Invoke(d);
        if (_lock <= 0f) _lock = 0.3f;
    }

    // ───────────────────────── scaling and scheduling ─────────────────────────

    private int N(BossMoveDef d) => Math.Max(1, d.Count + Mathf.FloorToInt(d.PerPhase * (Phase - d.Phase) + 0.001f));
    private int Volleys(BossMoveDef d) => Math.Max(1, d.Waves + Mathf.FloorToInt(d.WavesPerPhase * (Phase - d.Phase) + 0.001f));
    private int RingCount(BossMoveDef d) => Math.Max(0, d.Rings + Mathf.FloorToInt(d.RingsPerPhase * (Phase - d.Phase) + 0.001f));
    private float WindOf(BossMoveDef d) => Mathf.Max(0.6f, d.Wind / _windHaste);           // telegraphs never fill faster than 0.6 s
    private float BeatOf(BossMoveDef d) => Mathf.Max(0.6f, d.Interval / _windHaste);       // a telegraphed follow-up step
    private float RecOf(BossMoveDef d) => d.Recover / _haste;
    private float DmgOf(BossMoveDef d) => d.Dmg * _dmg;

    /// <summary>A step of the move in progress: dropped when the boss is staggered, changes phase or dies.</summary>
    private void Mv(float delay, Action a) => HazardClock.After(delay, a, _moveTok);
    /// <summary>A hazard that is already marked on the ground: resolves unless the boss dies or the fight resets.</summary>
    private void Hz(float delay, Action a) => HazardClock.After(delay, a, this);
    private void MoveMark(Node3D n) { if (n != null) _moveTeles.Add(n); }
    private void HazardMark(Node3D n) { if (n != null) _hazTeles.Add(n); }

    private Vector2 AimDir()
    {
        Vector2 d = _aim - _m.Pos2;
        return d.LengthSquared() < 0.01f ? _m.Forward : d.Normalized();
    }

    private Vector3 Muzzle() { var g = _m.GlobalPosition; return new Vector3(g.X, 1.3f, g.Z); }

    private static Vector2 InDisc(float radius)
    {
        float a = Rng.Range(0f, Mathf.Tau), r = Mathf.Sqrt(Rng.F()) * Mathf.Max(radius, 0f);
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }

    private static string CastSfx(Element el) => el switch
    {
        Element.Fire => "fire_cast", Element.Frost => "frost_cast", Element.Lightning => "lightning_cast",
        Element.Poison => "poison_cast", Element.Shadow => "shadow_cast", _ => "earth_cast",
    };

    private static string ImpactSfx(Element el) => el switch
    {
        Element.Fire => "fire_impact", Element.Frost => "frost_impact", Element.Lightning => "lightning_zap",
        Element.Poison => "poison_splash", Element.Shadow => "shadow_impact", _ => "earth_impact",
    };

    /// <summary>Starts an attack animation so that its blow lands when the telegraph is full.</summary>
    private void PlayWind(string anim, float contact, float wind)
    {
        var model = _m.Model;
        if (model == null) return;
        contact = CharModel.StrikeOr(anim, contact);       // when the clip really strikes (measured), not the estimate in the move list
        float speed = contact / Mathf.Max(wind, 0.05f);
        if (speed >= 0.55f) { model.Play(anim, Mathf.Min(speed, 1.6f), 0.12f, true); return; }
        // a long wind-up: hold a ready stance first, then swing at natural speed
        model.Play(_kit.Ranged ? "Spell_Simple_Enter" : "Sword_Idle", 1f, 0.2f, true);
        Mv(wind - contact, () => _m.Model?.Play(anim, 1f, 0.1f, true));
    }

    private void Strike(List<Actor> list, float mul, Element el, StatusApply? st, bool melee, bool once = false)
    {
        if (list.Count == 0) return;
        var arr = list.ToArray();                       // hits can re-enter the shared query list
        for (int i = 0; i < arr.Length; i++)
        {
            var a = arr[i];
            if (a == null || a.Dead || (once && !_struck.Add(a))) continue;
            Combat.MonsterHit(_m, a, mul, el, st, melee);
            if (GodotObject.IsInstanceValid(a)) Fx.Hit(a.Chest, el, false);
        }
    }

    /// <summary>Hits everyone standing between the two radii; the hole in the middle is safe.</summary>
    private void HitRing(Vector2 c, float inner, float outer, float mul, Element el, StatusApply? st)
    {
        World.InCircle(Faction.Monster, c, outer, Tmp);
        if (Tmp.Count == 0) return;
        var arr = Tmp.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            var a = arr[i];
            if (a == null || a.Dead) continue;
            float d = a.Pos2.DistanceTo(c);
            if (d < inner || d > outer) continue;
            Combat.MonsterHit(_m, a, mul, el, st);
            if (GodotObject.IsInstanceValid(a)) Fx.Hit(a.Chest, el, false);
        }
    }

    /// <summary>A marked circle that goes off after <paramref name="fuse"/> seconds and may leave a zone behind.</summary>
    private void Drop(BossMoveDef d, Vector2 at, float radius, float fuse)
    {
        HazardMark(Fx.Telegraph(at, radius, fuse, Palette.Danger, d.El));
        if (d.Meteor) Fx.Meteor(at, fuse, d.El, radius);
        Hz(fuse, () =>
        {
            Combat.MonsterArea(_m, at, radius, DmgOf(d), d.El, d.Status);
            Fx.Burst(World.To3(at, 0.2f), d.El, radius, 0.9f);
            Snd.At(d.HitSfx ?? ImpactSfx(d.El), World.To3(at), 0.7f);
            if (d.ZoneDur > 0f)
                _zones.Add(GroundZone.SpawnMonster(_m, at, radius * 0.92f, d.ZoneDur, d.ZoneDmg * _dmg, d.El, d.ZoneStatus));
            Dropped?.Invoke(d, at, radius);
        });
    }

    private void Shoot(BossMoveDef d, Vector2 dir, float dmgScale = 1f)
    {
        var shot = Projectile.SpawnMonster(_m, Muzzle(), dir, d.Speed, d.Reach, DmgOf(d) * dmgScale, d.El, d.Status, d.ShotRadius);
        if (d.Turn > 0f) shot.Homing = d.Turn;
        _shots.Add(shot);
    }

    // ───────────────────────── move library ─────────────────────────

    /// <summary>Frontal cone slam; with Count > 1 a combo of swings, each re-aimed and telegraphed on its own.</summary>
    private void DoCone(BossMoveDef d)
    {
        int n = N(d);
        float wind = WindOf(d), beat = BeatOf(d);
        _lock = wind + (n - 1) * beat + RecOf(d);
        Swing(d, 0, n, wind);
        for (int k = 1; k < n; k++)
        {
            int kk = k;
            Mv(wind + (k - 1) * beat, () => Swing(d, kk, n, beat));
        }
    }

    private void Swing(BossMoveDef d, int k, int n, float wind)
    {
        Vector2 dir = AimDir();
        if (k > 0)
        {
            // follow the target between swings so a combo cannot be out-walked backwards
            float gap = _m.Pos2.DistanceTo(_aim) - d.Radius * 0.55f;
            if (gap > 0.2f) _m.Move(dir * Mathf.Min(gap, 1.4f));
        }
        _m.FaceDir(dir);
        MoveMark(Fx.TelegraphCone(_m, d.Radius, d.Arc, wind, Palette.Danger, d.El));
        PlayWind(d.Anims != null ? d.Anims[k % d.Anims.Length] : d.Anim, d.Contact, wind);
        float mul = DmgOf(d) * (n > 1 && k == n - 1 ? 1.3f : 1f);     // a combo ends on its heaviest blow
        Mv(wind, () =>
        {
            World.InCone(Faction.Monster, _m.Pos2, _m.Forward, d.Radius, d.Arc, Tmp);
            Strike(Tmp, mul, d.El, d.Status, true);
            Fx.Slash(_m, d.Radius, d.Arc, d.El);
            Fx.Shake(n > 1 ? 0.18f : 0.25f, 0.2f);
            Snd.At(d.Sfx ?? "swing_heavy", _m.GlobalPosition);
        });
    }

    /// <summary>Circular slam around the boss, optionally followed by shock rings rolling outward and a radial burst.</summary>
    private void DoSlam(BossMoveDef d)
    {
        float wind = WindOf(d);
        int rings = RingCount(d);
        Vector2 c = _m.Pos2;
        _lock = wind + RecOf(d);
        MoveMark(Fx.Telegraph(c, d.Radius, wind, Palette.Danger, d.El));
        PlayWind(d.Anim, d.Contact, wind);
        if (d.Sfx != null) Snd.At(d.Sfx, _m.GlobalPosition, 0.8f);
        Mv(wind, () =>
        {
            Combat.MonsterArea(_m, c, d.Radius, DmgOf(d), d.El, d.Status);
            Fx.Burst(World.To3(c, 0.2f), d.El, d.Radius, 1.2f);
            Fx.Shake(0.45f, 0.3f);
            Snd.At(d.HitSfx ?? "boss_slam", _m.GlobalPosition);
            float gap = Mathf.Max(0.45f, 0.55f / _windHaste);
            for (int k = 0; k < rings; k++)
            {
                float inner = d.Radius + k * d.RingWidth;
                RingHazard(d, c, inner, inner + d.RingWidth, 0.7f + k * gap);
            }
            if (d.Shots > 0)
            {
                float a0 = Rng.Range(0f, Mathf.Tau);
                for (int k = 0; k < d.Shots; k++) Shoot(d, Vector2.Right.Rotated(a0 + k * Mathf.Tau / d.Shots), 0.45f);
            }
        });
    }

    private void RingHazard(BossMoveDef d, Vector2 c, float inner, float outer, float delay)
    {
        HazardMark(Fx.TelegraphRing(c, inner, outer, delay, Palette.Danger, d.El));
        Hz(delay, () =>
        {
            HitRing(c, inner, outer, d.RingDmg * _dmg, d.El, d.Status);
            Fx.Ring(World.To3(c, 0.25f), outer, Palette.Element(d.El), 0.45f, d.El);
            Snd.At(d.HitSfx ?? ImpactSfx(d.El), World.To3(c), 0.6f);
        });
    }

    /// <summary>Ring around the boss that is only safe close to it.</summary>
    private void DoDonut(BossMoveDef d)
    {
        float wind = WindOf(d);
        Vector2 c = _m.Pos2;
        float outer = d.Radius > d.Inner ? d.Radius : c.DistanceTo(_arena) + _arenaR + 4f;      // 0 = out to the arena wall
        _lock = wind + RecOf(d);
        _m.FaceDir(AimDir());
        MoveMark(Fx.TelegraphRing(c, d.Inner, outer, wind, Palette.Danger, d.El));
        PlayWind(d.Anim, d.Contact, wind);
        Fx.BuffAura(_m, d.El, wind, true);
        Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition);
        if (d.Warn != null) Game.I?.Hud?.Warn(d.Warn);
        Mv(wind, () =>
        {
            HitRing(c, d.Inner, outer, DmgOf(d), d.El, d.Status);
            Fx.Burst(World.To3(c, 0.3f), d.El, Mathf.Min(outer, 16f), 1.6f);
            Fx.Ring(World.To3(c, 0.25f), outer, Palette.Element(d.El), 0.6f, d.El);
            Fx.Shake(0.6f, 0.4f);
            Snd.At(d.HitSfx ?? "nova", _m.GlobalPosition);
        });
    }

    /// <summary>A marked lane the boss then travels along; Count lanes in a row, each aimed anew.</summary>
    private void DoCharge(BossMoveDef d)
    {
        int n = N(d);
        float wind = WindOf(d);
        const float travel = 0.45f;
        float beat = wind + travel + 0.2f;
        _lock = n * beat + RecOf(d);
        ChargeOnce(d, wind, travel);
        for (int k = 1; k < n; k++) Mv(k * beat, () => ChargeOnce(d, wind, travel));
    }

    private void ChargeOnce(BossMoveDef d, float wind, float travel)
    {
        Vector2 from = _m.Pos2, dir = AimDir();
        Vector2 want = from + dir * d.Length;
        Vector2 to = World.Grid != null ? World.Grid.ClampLine(from, want, false) : want;
        float len = from.DistanceTo(to);
        _m.FaceDir(dir);
        if (len < 3f) return;                           // no room to run: the beat passes as a feint
        MoveMark(Fx.TelegraphLine(from, dir, len, d.Width, wind, Palette.Danger, d.El));
        _m.Model?.Play("Sword_Idle", 1f, 0.15f, true);             // braced; the dash animation plays as it moves
        Snd.At("telegraph_warn", _m.GlobalPosition, 0.7f);
        Mv(wind, () =>
        {
            _struck.Clear();
            _mFrom = _m.Pos2; _mTo = to; _mT = 0f; _mDef = d;
            _mDur = Mathf.Max(0.18f, travel * len / Mathf.Max(d.Length, 1f));
            _motion = _chargeStep;
            _lock = Mathf.Max(_lock, _mDur + 0.1f);       // the run always finishes before the boss acts again
            _m.Model?.Play(d.Anim, 1.4f, 0.05f, true);
            Fx.DashTrail(_m, d.El, _mDur);
            Snd.At(d.Sfx ?? "swing_heavy", _m.GlobalPosition);
        });
    }

    private void ChargeStep(float dt)
    {
        var d = _mDef;
        Vector2 prev = _m.Pos2;
        _mT += dt;
        float k = Mathf.Clamp(_mT / _mDur, 0f, 1f);
        Vector2 cur = _mFrom.Lerp(_mTo, k);
        _m.Pos2 = cur;
        World.InCapsule(Faction.Monster, prev, cur, Mathf.Max(0.3f, d.Width * 0.5f - 0.15f), Tmp);
        Strike(Tmp, DmgOf(d), d.El, d.Status, true, true);
        if (k < 1f) return;
        _motion = null;
        Fx.Burst(World.To3(cur, 0.2f), d.El, 2.6f, 0.7f);
        Fx.Shake(0.3f, 0.2f);
        Snd.At(d.HitSfx ?? "boss_slam", _m.GlobalPosition, 0.8f);
    }

    /// <summary>Beams from the boss toward the hero: Count lines in a fan, optionally in several volleys.</summary>
    private void DoLine(BossMoveDef d)
    {
        int n = N(d), volleys = Volleys(d);
        float wind = WindOf(d), beat = BeatOf(d);
        _lock = wind + (volleys - 1) * beat + RecOf(d);
        LineVolley(d, n, wind);
        for (int v = 1; v < volleys; v++) Mv(wind + (v - 1) * beat, () => LineVolley(d, n, beat));
    }

    private void LineVolley(BossMoveDef d, int n, float wind)
    {
        Vector2 from = _m.Pos2, dir = AimDir();
        float spread = Mathf.DegToRad(d.Spread), half = Mathf.Max(0.3f, d.Width * 0.5f - 0.15f);
        _m.FaceDir(dir);
        PlayWind(d.Anim, d.Contact, wind);
        Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition, 0.8f);
        for (int k = 0; k < n; k++)
            MoveMark(Fx.TelegraphLine(from, dir.Rotated(spread * (k - (n - 1) * 0.5f)), d.Length, d.Width, wind, Palette.Danger, d.El));
        Mv(wind, () =>
        {
            _struck.Clear();
            for (int k = 0; k < n; k++)
            {
                Vector2 end = from + dir.Rotated(spread * (k - (n - 1) * 0.5f)) * d.Length;
                World.InCapsule(Faction.Monster, from, end, half, Tmp);
                Strike(Tmp, DmgOf(d), d.El, d.Status, false, true);
                Fx.Beam(World.To3(from, 1.2f), World.To3(end, 1.2f), d.El, d.Width * 0.5f, 0.3f);
            }
            Fx.Shake(0.2f, 0.2f);
            Snd.At(d.HitSfx ?? ImpactSfx(d.El), _m.GlobalPosition);
        });
    }

    /// <summary>Circles under the hero, one after the other; each is a hazard of its own once it is marked.</summary>
    private void DoEruptions(BossMoveDef d)
    {
        int n = N(d);
        float fuse = WindOf(d), gap = Mathf.Max(0.3f, d.Interval / _haste);
        _lock = 0.6f / _haste + RecOf(d);
        _m.FaceDir(AimDir());
        _m.Model?.Play(d.Anim, 1f, 0.12f, true);
        Fx.CastGlow(_m, d.El);
        Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition);
        for (int k = 0; k < n; k++) Mv(0.15f + k * gap, () => Drop(d, AimSpot(), d.Radius, fuse));
    }

    private Vector2 AimSpot()
    {
        var grid = World.Grid;
        return grid == null || grid.WalkAt(_aim) ? _aim : grid.NearestWalk(_aim);
    }

    /// <summary>Random circles over the arena for a while; every third one hunts the hero so standing still is no answer.</summary>
    private void DoRain(BossMoveDef d)
    {
        float fuse = WindOf(d), gap = Mathf.Max(0.2f, d.Interval / _haste);
        int per = N(d), ticks = Math.Max(1, Mathf.CeilToInt(d.Duration / gap));
        _lock = 0.9f / _haste + RecOf(d);
        _m.FaceDir(AimDir());
        _m.Model?.Play(d.Anim, 1f, 0.12f, true);
        Fx.CastGlow(_m, d.El);
        Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition);
        int fired = 0;
        HazardClock.Run(ticks * gap, k =>
        {
            int due = Math.Min(ticks, Mathf.FloorToInt(k * ticks) + 1);
            while (fired < due)
            {
                for (int j = 0; j < per; j++) Drop(d, RainSpot(j == 0 && fired % 3 == 0), d.Radius, fuse);
                fired++;
            }
        }, null, _moveTok);
    }

    private Vector2 RainSpot(bool hunt)
    {
        var grid = World.Grid;
        Vector2 centre = _aim.DistanceTo(_arena) <= _arenaR + 4f ? _arena : _m.Pos2;
        for (int i = 0; i < 6; i++)
        {
            Vector2 q = hunt ? _aim + InDisc(3f) : centre + InDisc(_arenaR - 1f);
            if (grid == null || grid.WalkAt(q)) return q;
        }
        return AimSpot();
    }

    /// <summary>Rings of circles closing on the spot where the hero stood: outer ring, inner ring, then the middle.</summary>
    private void DoPrison(BossMoveDef d)
    {
        int outer = N(d), inner = Math.Max(4, outer * 5 / 8);
        float fuse = WindOf(d), gap = Mathf.Max(0.5f, d.Interval / _haste);
        Vector2 c = AimSpot();
        float a0 = Rng.Range(0f, Mathf.Tau);
        _lock = 0.7f / _haste + RecOf(d);
        _m.FaceDir(AimDir());
        _m.Model?.Play(d.Anim, 1f, 0.12f, true);
        Fx.CastGlow(_m, d.El);
        Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition);
        for (int k = 0; k < outer; k++) Drop(d, c + Vector2.Right.Rotated(a0 + k * Mathf.Tau / outer) * d.Length, d.Radius, fuse);
        Mv(gap, () =>
        {
            for (int k = 0; k < inner; k++) Drop(d, c + Vector2.Right.Rotated(a0 + (k + 0.5f) * Mathf.Tau / inner) * (d.Length * 0.5f), d.Radius, fuse);
        });
        Mv(gap * 2f, () => Drop(d, c, d.Radius, fuse));
    }

    /// <summary>A fan of projectiles toward the hero; later volleys are re-aimed and fill the gaps of the one before.</summary>
    private void DoFan(BossMoveDef d)
    {
        int n = N(d), volleys = Volleys(d);
        float wind = WindOf(d), beat = BeatOf(d);
        _lock = wind + (volleys - 1) * beat + RecOf(d);
        FanVolley(d, n, wind, 0);
        for (int v = 1; v < volleys; v++)
        {
            int vv = v;
            Mv(wind + (v - 1) * beat, () => FanVolley(d, n, beat, vv));
        }
    }

    private void FanVolley(BossMoveDef d, int n, float wind, int volley)
    {
        Vector2 from = _m.Pos2, dir = AimDir();
        float spread = Mathf.DegToRad(d.Spread);
        float skew = volley % 2 == 1 && n > 1 ? spread * 0.5f : 0f;
        float show = Mathf.Min(d.Reach, 12f), width = d.ShotRadius * 2f + 0.3f;
        _m.FaceDir(dir);
        PlayWind(d.Anim, d.Contact, wind);
        for (int k = 0; k < n; k++)
            MoveMark(Fx.TelegraphLine(from, dir.Rotated(skew + spread * (k - (n - 1) * 0.5f)), show, width, wind, Palette.Danger, d.El));
        Mv(wind, () =>
        {
            for (int k = 0; k < n; k++) Shoot(d, dir.Rotated(skew + spread * (k - (n - 1) * 0.5f)));
            Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition, 0.8f);
        });
    }

    /// <summary>Channelled spiral: Count arms of slow projectiles turning by Rotate degrees per shot.</summary>
    private void DoSpiral(BossMoveDef d)
    {
        float wind = WindOf(d), gap = Mathf.Max(0.1f, d.Interval);
        int arms = N(d), ticks = Math.Max(1, Mathf.CeilToInt(d.Duration / gap));
        float dur = ticks * gap;
        _lock = wind + dur + RecOf(d);
        MoveMark(Fx.Telegraph(_m.Pos2, _m.Radius + 2f, wind, Palette.Danger, d.El));
        _m.Model?.Play("Spell_Simple_Enter", 1f, 0.15f, true);
        Fx.CastGlow(_m, d.El);
        Fx.BuffAura(_m, d.El, wind + dur, false);
        Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition);
        Mv(wind, () =>
        {
            _m.Model?.Play("Spell_Simple_Idle_Loop", 1f, 0.15f);
            float a0 = Rng.Range(0f, Mathf.Tau), turn = Mathf.DegToRad(d.Rotate) * (Rng.Chance(0.5f) ? 1f : -1f);
            int fired = 0;
            HazardClock.Run(dur, k =>
            {
                int due = Math.Min(ticks, Mathf.FloorToInt(k * ticks) + 1);
                while (fired < due)
                {
                    for (int a = 0; a < arms; a++) Shoot(d, Vector2.Right.Rotated(a0 + fired * turn + a * Mathf.Tau / arms));
                    if (fired % 4 == 0) Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition, 0.5f);
                    fired++;
                }
            }, null, _moveTok);
        });
    }

    /// <summary>Slow orbs thrown out to the sides that curve after the hero.</summary>
    private void DoHoming(BossMoveDef d)
    {
        int n = N(d);
        float wind = WindOf(d);
        const float gap = 0.3f;
        _lock = wind + (n - 1) * gap + RecOf(d);
        _m.FaceDir(AimDir());
        MoveMark(Fx.Telegraph(_m.Pos2, _m.Radius + 1.6f, wind, Palette.Danger, d.El));
        PlayWind(d.Anim, d.Contact, wind);
        Fx.CastGlow(_m, d.El);
        for (int k = 0; k < n; k++)
        {
            float side = (k % 2 == 0 ? 1f : -1f) * (0.7f + 0.25f * (k / 2));
            Mv(wind + k * gap, () =>
            {
                Shoot(d, AimDir().Rotated(side));
                Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition, 0.7f);
            });
        }
    }

    /// <summary>Count lines crossing at the boss (or at the hero); with several steps the pattern turns by Rotate each time.</summary>
    private void DoCross(BossMoveDef d)
    {
        int lines = Math.Max(2, N(d)), steps = Volleys(d);
        float wind = WindOf(d), beat = BeatOf(d);
        Vector2 aim = AimDir();
        float a0 = Mathf.Atan2(aim.Y, aim.X) + (d.AtHero ? Mathf.Pi / (2f * lines) : 0f);      // boss-centred: one arm starts on the hero
        float turn = Mathf.DegToRad(d.Rotate) * (Rng.Chance(0.5f) ? 1f : -1f);
        _lock = wind + (steps - 1) * beat + RecOf(d);
        _m.FaceDir(aim);
        Snd.At(d.Sfx ?? CastSfx(d.El), _m.GlobalPosition);
        CrossStep(d, lines, a0, wind, 0);
        for (int s = 1; s < steps; s++)
        {
            int ss = s;
            Mv(wind + (s - 1) * beat, () => CrossStep(d, lines, a0 + ss * turn, beat, ss));
        }
    }

    private void CrossStep(BossMoveDef d, int lines, float ang, float wind, int step)
    {
        Vector2 c = d.AtHero ? AimSpot() : _m.Pos2;
        float half = Mathf.Max(0.3f, d.Width * 0.5f - 0.15f);
        if (d.AtHero) _m.FaceDir(AimDir());
        PlayWind(d.Anims != null ? d.Anims[step % d.Anims.Length] : d.Anim, d.Contact, wind);
        for (int k = 0; k < lines; k++)
        {
            Vector2 dir = Vector2.Right.Rotated(ang + k * Mathf.Pi / lines);
            MoveMark(Fx.TelegraphLine(c - dir * d.Length, dir, d.Length * 2f, d.Width, wind, Palette.Danger, d.El));
        }
        Mv(wind, () =>
        {
            _struck.Clear();                            // standing where two lines cross is still one hit
            for (int k = 0; k < lines; k++)
            {
                Vector2 dir = Vector2.Right.Rotated(ang + k * Mathf.Pi / lines);
                Vector2 a = c - dir * d.Length, b = c + dir * d.Length;
                World.InCapsule(Faction.Monster, a, b, half, Tmp);
                Strike(Tmp, DmgOf(d), d.El, d.Status, false, true);
                Fx.Beam(World.To3(a, 0.9f), World.To3(b, 0.9f), d.El, d.Width * 0.5f, 0.25f);
            }
            Fx.Shake(0.25f, 0.2f);
            Snd.At(d.HitSfx ?? ImpactSfx(d.El), World.To3(c));
        });
    }

    /// <summary>Calls adds from the move's named defs, or from the level's region table when none are named.</summary>
    private void DoSummon(BossMoveDef d)
    {
        PruneAdds();
        int n = Math.Min(N(d), AddCap - _adds.Count);
        float wind = WindOf(d);
        _lock = 0.8f / _haste + RecOf(d);
        _m.Model?.Play(d.Anim, 1f, 0.12f, true);
        Fx.CastGlow(_m, d.El);
        Snd.At(d.Sfx ?? "summon_raise", _m.GlobalPosition);
        if (n > 0) SpawnAdds(d.Adds, n, wind);
    }

    private void SpawnAdds(string[] ids, int n, float delay)
    {
        var grid = World.Grid;
        for (int k = 0; k < n; k++)
        {
            MonsterDef def = ids != null && ids.Length > 0 ? MonsterDb.Get(ids[k % ids.Length]) : RegionAdd();
            def ??= MonsterDb.Get("skeleton");
            if (def == null) continue;
            Vector2 at = grid != null ? grid.RandomWalkNear(_m.Pos2, 3f, 6.5f) : _m.Pos2 + Vector2.Right.Rotated(Rng.Range(0f, Mathf.Tau)) * 4.5f;
            HazardMark(Fx.Telegraph(at, 1.1f, delay, Palette.Danger, Sigil.Arcane));
            Hz(delay, () =>
            {
                var add = Monster.Spawn(def, _m.Level, MonTier.Normal, at, Faction.Monster);
                add.NoLoot = true; add.XpValue = 0f;     // a boss's servants are not a harvest
                add.ForceAggro();
                _adds.Add(add);
                Fx.Dust(World.To3(at));
                Fx.Burst(World.To3(at, 0.3f), _kit.El, 1.2f, 0.4f);
            });
        }
    }

    /// <summary>A weighted pick from the region's spawn table; walking bombs are left out of boss fights.</summary>
    private static MonsterDef RegionAdd()
    {
        var table = Game.I?.Level?.Region?.Spawns;
        if (table == null || table.Length == 0) return null;
        float total = 0f;
        for (int i = 0; i < table.Length; i++) total += AddWeight(table[i].id, table[i].w);
        if (total <= 0f) return null;
        float r = Rng.F() * total;
        for (int i = 0; i < table.Length; i++)
        {
            float w = AddWeight(table[i].id, table[i].w);
            if (w <= 0f) continue;
            r -= w;
            if (r <= 0f) return MonsterDb.Get(table[i].id);
        }
        return null;
    }

    private static float AddWeight(string id, float w)
    {
        var def = MonsterDb.Get(id);
        return def == null || def.AI == MonAI.Exploder || def.AI == MonAI.Boss ? 0f : Mathf.Max(w, 0f);
    }

    /// <summary>Blink to another spot of the arena; "away" picks the far side from the hero.</summary>
    private void DoTeleport(BossMoveDef d)
    {
        const float wind = 0.6f;
        Vector2 to = d.Away ? FarSpot() : World.Grid != null ? World.Grid.RandomWalkNear(_aim, 2.5f, 4f) : _aim + InDisc(3f);
        if (to.DistanceTo(_m.Pos2) < 2f) { _lock = 0.2f; return; }
        _lock = wind + RecOf(d);
        MoveMark(Fx.Telegraph(to, _m.Radius + 0.8f, wind, Palette.Danger, d.El));
        _m.Model?.Play("Spell_Simple_Enter", 1.4f, 0.1f, true);
        Fx.CastGlow(_m, d.El);
        Mv(wind, () =>
        {
            Fx.Teleport(_m.Chest, d.El);
            Snd.At("teleport", _m.GlobalPosition, 0.8f);
            _m.Pos2 = to;
            Fx.Teleport(_m.Chest, d.El);
            _m.FaceDir(_aim - to);
        });
    }

    private Vector2 FarSpot()
    {
        var grid = World.Grid;
        Vector2 best = _m.Pos2;
        float bestScore = -1f;
        for (int i = 0; i < 12; i++)
        {
            Vector2 q = _arena + InDisc(_arenaR - 2f);
            if (grid != null && !grid.WalkAt(q)) continue;
            float score = Mathf.Min(q.DistanceTo(_aim), 13f) + (grid == null || grid.LineShot(q, _aim) ? 3f : 0f);
            if (score > bestScore) { bestScore = score; best = q; }
        }
        return best;
    }

    /// <summary>The boss vanishes, a circle fills where the hero stands, and it reappears there with a slam.</summary>
    private void DoTeleStrike(BossMoveDef d)
    {
        float wind = WindOf(d);
        Vector2 at = AimSpot();
        _lock = wind + RecOf(d);
        Fx.Teleport(_m.Chest, d.El);
        Snd.At("teleport", _m.GlobalPosition, 0.8f);
        Vanish(true);
        MoveMark(Fx.Telegraph(at, d.Radius, wind, Palette.Danger, d.El));
        Mv(wind, () =>
        {
            _m.Pos2 = at;
            Vanish(false);
            _m.FaceDir(_aim - at);
            _m.Model?.Play(d.Anim, 1.5f, 0.05f, true);
            Fx.Teleport(_m.Chest, d.El);
            Combat.MonsterArea(_m, at, d.Radius, DmgOf(d), d.El, d.Status);
            Fx.Burst(World.To3(at, 0.2f), d.El, d.Radius, 1.2f);
            Fx.Shake(0.45f, 0.3f);
            Snd.At(d.HitSfx ?? "boss_slam", _m.GlobalPosition);
        });
    }

    private void Vanish(bool on)
    {
        _vanished = on;
        _m.Visible = !on;
        _m.Untargetable = on || _roaring || _m.Dead;
    }

    /// <summary>A tether lane toward the hero drags whoever stands in it to the boss, then the boss slams around itself.</summary>
    private void DoPullSlam(BossMoveDef d)
    {
        float warn = Mathf.Max(0.7f, 0.8f / _windHaste), wind = WindOf(d);
        Vector2 from = _m.Pos2, dir = AimDir();
        float reach = Mathf.Min(from.DistanceTo(_aim) + 3f, d.Length);
        _lock = warn + wind + RecOf(d);
        _m.FaceDir(dir);
        MoveMark(Fx.TelegraphLine(from, dir, reach, d.Width, warn, Palette.Danger, d.El));
        _m.Model?.Play("Spell_Simple_Enter", 1f, 0.15f, true);
        Fx.CastGlow(_m, d.El);
        Snd.At("wind_cast", _m.GlobalPosition);
        Mv(warn, () =>
        {
            var p = World.P;
            if (p != null && !p.Dead && !p.Invulnerable && World.DistToSegment(p.Pos2, from, from + dir * reach) <= d.Width * 0.5f + p.Radius)
            {
                float pull = p.Pos2.DistanceTo(_m.Pos2) - (_m.Radius + p.Radius + 0.8f);
                if (pull > 0.3f)
                {
                    p.ApplyStatus(St.Pull(Mathf.Sqrt(56f * pull)), _m);       // knock-back velocity decays at 28 m/s²
                    Fx.Beam(_m.Chest, p.Chest, d.El, 0.35f, 0.4f);
                    Snd.At("chain", p.GlobalPosition, 0.9f);
                }
            }
            Vector2 c = _m.Pos2;
            MoveMark(Fx.Telegraph(c, d.Radius, wind, Palette.Danger, d.El));
            PlayWind(d.Anim, d.Contact, wind);
            Mv(wind, () =>
            {
                Combat.MonsterArea(_m, c, d.Radius, DmgOf(d), d.El, d.Status);
                Fx.Burst(World.To3(c, 0.2f), d.El, d.Radius, 1.3f);
                Fx.Shake(0.5f, 0.3f);
                Snd.At(d.HitSfx ?? "boss_slam", _m.GlobalPosition);
            });
        });
    }

    /// <summary>A jump onto a marked circle near the hero.</summary>
    private void DoLeap(BossMoveDef d)
    {
        float wind = WindOf(d), air = Mathf.Min(0.55f, wind * 0.6f);
        Vector2 from = _m.Pos2, land = AimSpot();
        Vector2 dir = land - from;
        float len = dir.Length();
        if (len > 2.5f)
        {
            // come down just short of the target instead of inside it
            Vector2 shy = from + dir / len * (len - 1.2f);
            if (World.Grid == null || World.Grid.WalkAt(shy)) land = shy;
        }
        _lock = wind + RecOf(d);
        _m.FaceDir(dir);
        MoveMark(Fx.Telegraph(land, d.Radius, wind, Palette.Danger, d.El));
        _m.Model?.Play("NinjaJump_Start", 1.2f, 0.1f, true);
        Snd.At("telegraph_warn", _m.GlobalPosition, 0.7f);
        Mv(wind - air, () =>
        {
            _mFrom = _m.Pos2; _mTo = land; _mT = 0f; _mDur = air; _mDef = d;
            _motion = _leapStep;
            _lock = Mathf.Max(_lock, air + 0.1f);          // the jump always lands before the boss acts again
            _m.Model?.Play("NinjaJump_Idle_Loop", 1f, 0.05f);
            Snd.At(d.Sfx ?? "swing_heavy", _m.GlobalPosition, 0.8f);
        });
    }

    private void LeapStep(float dt)
    {
        var d = _mDef;
        _mT += dt;
        float k = Mathf.Clamp(_mT / _mDur, 0f, 1f);
        Vector2 q = _mFrom.Lerp(_mTo, k);
        if (k < 1f) { _m.GlobalPosition = new Vector3(q.X, Mathf.Sin(k * Mathf.Pi) * 3f, q.Y); return; }
        _motion = null;
        _m.GlobalPosition = new Vector3(q.X, 0f, q.Y);
        _m.Model?.Play("NinjaJump_Land", 1.5f, 0.05f, true);
        Combat.MonsterArea(_m, q, d.Radius, DmgOf(d), d.El, d.Status);
        Fx.Burst(World.To3(q, 0.2f), d.El, d.Radius, 1.3f);
        Fx.Shake(0.5f, 0.3f);
        Snd.At(d.HitSfx ?? "leap_land", _m.GlobalPosition);
    }

    // ───────────────────────── clean-up ─────────────────────────

    /// <summary>Drops the move in progress: its pending steps, its telegraphs and any half-finished movement.</summary>
    private void CancelMove()
    {
        HazardClock.Cancel(_moveTok);
        FreeAll(_moveTeles);
        _motion = null; _cur = null; _lock = 0f;
        if (_vanished) Vanish(false);
        Ground();
    }

    /// <summary>Drops everything already on the field: marked hazards, lingering zones and projectiles in flight.</summary>
    private void ClearHazards()
    {
        HazardClock.Cancel(this);
        FreeAll(_hazTeles);
        for (int i = 0; i < _zones.Count; i++)
        {
            var z = _zones[i];
            if (z != null && GodotObject.IsInstanceValid(z) && !z.Done) z.Remove();
        }
        _zones.Clear();
        for (int i = 0; i < _shots.Count; i++)
        {
            var s = _shots[i];
            if (s != null && GodotObject.IsInstanceValid(s) && !s.IsQueuedForDeletion()) Fx.ReleaseProjectile(s);
        }
        _shots.Clear();
    }

    private static void FreeAll(List<Node3D> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n != null && GodotObject.IsInstanceValid(n) && !n.IsQueuedForDeletion()) n.QueueFree();
        }
        nodes.Clear();
    }

    /// <summary>Back on the floor and on a walkable cell after an interrupted leap.</summary>
    private void Ground()
    {
        var g = _m.GlobalPosition;
        if (Mathf.Abs(g.Y) < 0.001f) return;
        _m.GlobalPosition = new Vector3(g.X, 0f, g.Z);
        var grid = World.Grid;
        if (grid != null && !grid.WalkAt(_m.Pos2)) _m.Pos2 = grid.NearestWalk(_m.Pos2);
    }

    private void Prune()
    {
        _moveTeles.RemoveAll(GoneNode);
        _hazTeles.RemoveAll(GoneNode);
        _zones.RemoveAll(GoneZone);
        _shots.RemoveAll(GoneShot);
    }

    private void PruneAdds() => _adds.RemoveAll(GoneAdd);

    /// <summary>The adds crumble with their master (or when the fight resets); they leave nothing behind.</summary>
    private void KillAdds()
    {
        if (_adds.Count == 0) return;
        var arr = _adds.ToArray();
        _adds.Clear();
        for (int i = 0; i < arr.Length; i++)
        {
            var a = arr[i];
            if (a == null || !GodotObject.IsInstanceValid(a) || a.Dead) continue;
            a.NoLoot = true; a.XpValue = 0f;
            a.Life = 0f; a.Dead = true;
            a.Die(new Hit());
        }
    }
}
