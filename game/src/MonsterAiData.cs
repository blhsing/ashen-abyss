using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Per-type values of the behaviour data fields (BESTIARY.md section 5 and the status column of section 2): which
/// status the hook, the waking strike, the death burst, the breath … of each type applies, what its bombs and trails
/// leave on the ground, whom it summons. The numbers the specification gives as defaults live in <see cref="MonAiData"/>.
/// </summary>
public static class MonsterAiTune
{
    private static MonZone Zone(float r, float dur, Element el, StatusApply? st = null, float mul = 0.15f) => new() { Radius = r, Dur = dur, El = el, Status = st, Mul = mul };

    /// <summary>A new bestiary type: the ordinary statuses of its hits from the status text, then what its kind and traits need.</summary>
    public static void Apply(MonsterDef d, BestiaryType t, bool builtIn)
    {
        var a = d.Ai;
        if (d.Is(MonTrait.Dormant))
        {
            a.DormantMode = Bestiary.DormantModeOf(t);
            a.WakeRadius = a.DormantMode == "statue" ? 4.5f : 5.5f;
        }
        if (builtIn) return;        // the definitions written by hand keep their tuned statuses
        var sts = Bestiary.ParseStatuses(t.Status);
        d.OnHit = sts.Count > 0 ? sts[0] : null;
        d.OnHit2 = sts.Count > 1 ? sts[1] : null;

        switch (d.Id)
        {
            // ── bone ──
            case "bone_pallbearer": d.OnHit = St.Knock(6f); break;
            case "bone_urnbearer": a.ReviveFamilies = new[] { "bone" }; break;
            // ── husk ──
            case "husk_chained": d.OnHit = null; a.HookStatus = St.Slow(0.3f, 2f); break;
            case "husk_huddle": a.SplitDef = "husk"; a.SplitCount = 3; break;
            case "husk_wailer": d.OnHit = null; a.CallDef = "husk"; a.CallWail = St.Fear(1f); a.CallWailRadius = 4f; a.CastAnim = "Spell_Simple_Enter"; break;
            // ── cult ──
            case "cult_snuffer": d.OnHit = St.Weaken(0.25f, 4f); break;
            case "cult_assassin": d.OnHit = null; a.PounceStatus = St.Vuln(3f); break;
            case "cult_censer": a.HealFilter = null; break;
            // ── wraith ──
            case "wraith_mourner": d.OnHit = St.Chill(0.3f); d.OnHit2 = new StatusApply(StatusId.Fear, 0.8f, 0, 0.2f); break;
            case "wraith_gasp": a.BurstRadius = 1.8f; a.BurstDmg = 0.6f; a.BurstDelay = 0.4f; a.BurstStatus = St.Chill(0.3f); break;
            // ── reaver ──
            case "reaver_hook": d.OnHit = null; a.HookStatus = St.Slow(0.3f, 1.5f); break;
            case "reaver_firepot": d.OnHit = St.Burn(0.3f, 3f); a.BombZone = Zone(2.4f, 4f, Element.Fire, St.Burn(0.3f, 3f)); break;
            case "reaver_cagebearer": a.AuraEffect = "haste"; a.AuraRadius = 8f; break;
            case "reaver_brute": d.OnHit = St.Knock(6f); d.OnHit2 = St.Stun(0.5f); break;
            case "reaver_houndmaster": a.CallDef = "dusk_hound"; a.EscortDefs = new[] { "dusk_hound", "dusk_hound" }; break;
            // ── dusk ──
            case "dusk_boar": d.OnHit = St.Knock(6f); break;
            case "dusk_yak": d.OnHit = St.Chill(0.4f); d.OnHit2 = St.Knock(6f); break;
            case "dusk_lynx": d.OnHit = St.Bleed(0.3f, 3f); a.PounceStatus = St.Vuln(2f); break;
            case "dusk_crow": d.OnHit = St.Weaken(0.15f, 3f); break;
            case "dusk_bat": d.OnHit = St.Bleed(0.25f, 3f); break;
            case "dusk_gadfly": d.OnHit = St.Poison(0.3f, 3f); a.DrainHeal = true; break;
            case "dusk_moths": d.OnHit = St.Weaken(0.2f, 1.2f); a.Attract = "fire"; break;
            // ── frost ──
            case "rime_horn": d.OnHit = St.Knock(6f); a.BreathMode = "chill"; a.BreathStatus = St.Chill(0.3f); a.CastAnim = "Spell_Simple_Enter"; break;
            case "rime_sentry": d.OnHit = St.Chill(0.4f); a.WakeStatus = St.Root(1f); a.ShellMul = 1f; break;
            case "rime_breaker": d.OnHit = St.Chill(0.5f); break;
            case "rime_packbeast": d.OnHit = St.Chill(0.5f); a.BurstRadius = 4.5f; a.BurstDmg = 1.6f; a.BurstStatus = St.Freeze(1f); break;
            // ── coldfire ──
            case "cf_flicker": d.OnHit = St.Chill(0.25f); a.BurstRadius = 2f; a.BurstDmg = 0.7f; a.BurstDelay = 0.4f; a.BurstStatus = St.Chill(0.3f); break;
            case "cf_spirit": d.OnHit = St.Chill(0.35f); a.Trail = Zone(1.8f, 4f, Element.Frost, St.Chill(0.15f), 0.1f); break;
            case "cf_hulk": d.OnHit = St.Chill(0.5f); a.HookStatus = St.Chill(0.3f); a.AuraEffect = "chill"; a.AuraRadius = 5f; break;
            // ── hauler ──
            case "hauler_yoke": d.OnHit = St.Knock(7f); break;
            case "hauler_hod": d.OnHit = St.Knock(6f); a.BombDirect = St.Stun(0.6f); a.BombMelee = true; a.BombZone = Zone(2.4f, 3f, Element.Physical, St.Slow(0.3f, 1f), 0.05f); break;
            case "hauler_collector": d.OnHit = null; a.HoldTime = 2f; a.GrabThrow = true; break;
            case "hauler_stoker": d.OnHit = St.Burn(0.4f, 3f); a.BombMelee = true; a.BombZone = Zone(2.4f, 5f, Element.Fire, St.Burn(0.4f, 3f)); break;
            // ── sexton ──
            case "sexton_digger": d.OnHit = null; a.EmergeStatus = St.Root(1.5f); break;
            case "sexton_matron": d.OnHit = St.Knock(6f); a.SpawnDef = "sexton_grub"; break;
            // ── char ──
            case "char_stump": d.OnHit = null; a.LitBurn = St.Burn(0.2f, 3f); break;
            case "char_walker": d.OnHit = St.Slow(0.2f, 2f); a.LitBurn = St.Burn(0.3f, 3f); break;
            case "char_hangtree": d.OnHit = St.Knock(6f); a.HookStatus = St.Root(1f); a.LitBurn = St.Burn(0.3f, 3f); a.CastAnim = "OverhandThrow"; break;
            case "char_root": d.OnHit = null; a.EmergeStatus = St.Root(1.2f); a.StationaryEmerge = true; a.LitBurn = St.Burn(0.3f, 3f); break;
            // ── ash ──
            case "ash_cast": d.OnHit = St.Weaken(0.15f, 3f); break;
            case "ash_weeper": d.OnHit = St.Weaken(0.25f, 4f); a.EruptZone = Zone(2.2f, 6f, Element.Shadow, St.Slow(0.3f, 1f), 0.05f); break;
            case "ash_colossus": d.OnHit = St.Weaken(0.3f, 4f); d.OnHit2 = St.Knock(6f); a.SplitDef = "ash_cast"; a.SplitCount = 3; break;
            case "ash_worm": d.OnHit = St.Weaken(0.2f, 3f); a.BreathMode = "status"; a.BreathStatus = St.Slow(0.4f, 2f); break;
            // ── shroud ──
            case "shroud_weaver": d.OnHit = St.Slow(0.3f, 3f); a.SnareRoot = St.Root(1.5f); break;
            case "shroud_dropper": d.OnHit = St.Poison(0.3f, 3f); d.OnHit2 = St.Slow(0.3f, 2f); break;
            case "shroud_matron": d.OnHit = St.Poison(0.3f, 4f); d.OnHit2 = St.Knock(6f); a.SpawnDef = "shroud_ling"; a.EggSac = true; break;
            // ── effigy ──
            case "effigy_caryatid": d.OnHit = St.Knock(6f); a.BombDirect = St.Stun(0.6f); a.BombMelee = true; a.BombZone = Zone(2.4f, 3f, Element.Physical, St.Slow(0.3f, 1f), 0.05f); break;
            case "effigy_warden": d.OnHit = St.Bleed(0.3f, 3f); a.WakeStatus = St.Vuln(2f); break;
            case "effigy_hound": d.OnHit = St.Knock(5f); a.WakeStatus = St.Slow(0.3f, 2f); break;
            // ── mire ──
            case "bog_netter": d.OnHit = null; a.SnareRoot = St.Root(1.8f); break;
            case "bog_sleeper": d.OnHit = St.Slow(0.3f, 2f); d.OnHit2 = St.Poison(0.2f, 3f); break;
            case "bog_toad": d.OnHit = St.Poison(0.3f, 3f); a.HookStatus = St.Slow(0.3f, 2f); break;
            // ── lamp ──
            case "lamp_lure": d.OnHit = St.Poison(0.3f, 3f); a.Attack2 = "cloud"; break;
            case "lamp_bomber": d.OnHit = St.Weaken(0.2f, 3f); a.BombZone = Zone(2.4f, 5f, Element.Poison, St.Poison(0.25f, 3f)); break;
            case "lamp_crawler": d.OnHit = St.Poison(0.2f, 3f); a.BurstRadius = 2f; a.BurstDmg = 0.3f; a.BurstDelay = 0.4f; a.BurstZone = Zone(2f, 3f, Element.Poison, St.Poison(0.2f, 3f)); break;
            case "lamp_colony": d.OnHit = St.Poison(0.4f, 4f); a.AuraEffect = "weaken"; a.AuraRadius = 5f; break;
            // ── lurk ──
            case "lurk_leech": d.OnHit = St.Chill(0.2f); d.OnHit2 = St.Slow(0.2f, 2f); a.DrainHeal = true; break;
            case "lurk_spitter": d.OnHit = St.Poison(0.4f, 4f); a.BombZone = Zone(2.4f, 4f, Element.Poison, St.Poison(0.3f, 3f)); break;
            case "lurk_elder": d.OnHit = St.Chill(0.5f); a.SplitDef = "lurk_leech"; a.SplitCount = 4; break;
            // ── sands ──
            case "dune_mirror": d.OnHit = St.Stun(0.5f); break;
            case "dune_stormjar": d.OnHit = St.Stun(0.3f, 0.2f); a.BurstRadius = 4f; a.BurstDmg = 1f; a.BurstEl = Element.Lightning; break;
            case "dune_masker": a.CastAnim = CharModel.HasClip("Interact") ? "Interact" : "Spell_Simple_Shoot"; break;
            // ── rust ──
            case "rust_stinger": d.OnHit = St.Vuln(3f); d.OnHit2 = St.Poison(0.3f, 3f); break;
            case "rust_gear": d.OnHit = St.Knock(6f); d.OnHit2 = St.Bleed(0.2f, 3f); break;
            case "rust_jar": d.OnHit = St.Stun(0.3f, 0.2f); a.BurstRadius = 3f; a.BurstDmg = 0.8f; a.BurstEl = Element.Lightning; break;
            // ── engine ──
            case "engine_heliostat": d.OnHit = St.Burn(0.5f, 3f); a.Attack2 = "flash"; break;
            case "engine_carrier": d.OnHit = St.Stun(0.6f); d.OnHit2 = St.Knock(6f); break;
            case "engine_winder": a.HealFilter = "construct"; a.AuraEffect = "windup"; a.AuraRadius = 8f; a.CastAnim = CharModel.HasClip("Interact") ? "Interact" : "Spell_Simple_Shoot"; break;
            // ── wax ──
            case "wax_sealed": d.OnHit = St.Slow(0.3f, 2f); a.LitBurn = St.Burn(0.2f, 3f); break;
            case "wax_wick": d.OnHit = St.Burn(0.5f, 4f); a.Trail = Zone(3.6f, 5f, Element.Fire, St.Slow(0.3f, 1f)); break;
            case "wax_dripper": d.OnHit = St.Slow(0.5f, 3f); a.Trail = new MonZone { Radius = 2.5f, Dur = 6f, El = Element.Physical, Status = St.Slow(0.5f, 1f), Mul = 0.04f, RootOnReentry = 1f }; break;
            case "wax_chandler": d.OnHit = St.Slow(0.4f, 3f); d.OnHit2 = St.Burn(0.2f, 3f); a.Trail = Zone(2f, 5f, Element.Fire, St.Slow(0.4f, 1f), 0.08f); break;
            // ── bell ──
            case "bell_hood": d.OnHit = St.Knock(6f); a.ChargeStatus = St.Stun(0.6f); break;
            case "bell_clapper": d.OnHit = St.Stun(0.8f); a.PulseStatus = St.Weaken(0.2f, 3f); a.PulseRings = 1; break;
            case "bell_hanging": d.OnHit = St.Weaken(0.3f, 4f); a.PulseStatus = St.Weaken(0.3f, 4f); a.PulseNear = St.Stun(0.5f); a.PulseNearRadius = 3f; a.PulseRings = 2; break;
            // ── fiend ──
            case "fiend_wing": d.OnHit = St.Burn(0.3f, 3f); a.Trail = Zone(1.6f, 3f, Element.Fire, St.Burn(0.2f, 3f)); break;
            case "fiend_maw": d.OnHit = St.Burn(0.4f, 3f); d.OnHit2 = St.Knock(5f); a.BreathMode = "status"; a.BreathStatus = St.Burn(0.2f, 3f); break;
            case "fiend_caller": d.OnHit = St.Burn(0.4f, 3f); a.CallDef = "imp"; a.CastAnim = CharModel.HasClip("Spell_Simple_Enter") ? "Spell_Simple_Enter" : "Spell_Simple_Shoot"; break;
            // ── forge ──
            case "forge_bellows": d.OnHit = St.Burn(0.2f, 3f); a.BreathMode = "knock"; a.BreathStatus = St.Burn(0.2f, 3f); a.CastAnim = "Spell_Simple_Enter"; break;
            case "forge_tongs": d.OnHit = St.Bleed(0.2f, 3f); a.HoldTime = 1.2f; break;
            case "forge_pourer": d.OnHit = St.Burn(0.4f, 3f); a.BombZone = Zone(2.4f, 5f, Element.Fire, St.Burn(0.4f, 3f)); break;
            case "forge_hammer": d.OnHit = St.Stun(0.6f); d.OnHit2 = St.Knock(6f); a.PulseStatus = St.Stun(0.6f); a.PulseRings = 1; break;
            case "forge_eye": d.OnHit = St.Burn(0.5f, 3f); a.Shutter = true; a.Attack2 = "fan"; break;
            case "forge_hound": d.OnHit = St.Burn(0.2f, 3f); break;
            // ── hush ──
            case "hush_blank": d.OnHit = St.Weaken(0.2f, 3f); break;
            case "hush_mouth": d.OnHit = St.Weaken(0.25f, 3f); a.CastAnim = "Spell_Simple_Enter"; break;
            case "hush_mute": d.OnHit = St.Weaken(0.3f, 4f); d.OnHit2 = St.Slow(0.4f, 3f); a.PulseStatus = St.Weaken(0.3f, 4f); a.PulseRings = 2; break;
            case "hush_ribwalker": d.OnHit = St.Weaken(0.1f, 2f); break;
            case "hush_beast": d.OnHit = null; a.PounceStatus = St.Vuln(3f); break;
            case "hush_serpent": d.OnHit = St.Weaken(0.25f, 3f); d.OnHit2 = St.Slow(0.3f, 2f); break;
            case "hush_wing": d.OnHit = St.Weaken(0.2f, 3f); break;
        }
        // a clip the library lacks would be an error at play time: fall back to the plain cast
        if (d.Rig == "A" && !CharModel.HasClip(a.CastAnim)) a.CastAnim = "Spell_Simple_Shoot";
        if (d.Is(MonTrait.Flammable) && !a.LitBurn.HasValue) a.LitBurn = St.Burn(0.2f, 3f);
    }

    /// <summary>A regional reskin: same body and behaviour, the element and statuses of its region (BESTIARY.md 3.9).</summary>
    public static void ApplyReskin(MonsterDef d, MonsterDef baseDef, BestiaryReskin r)
    {
        var a = d.Ai;
        Element el = d.Element;
        // ground left behind follows the new element
        if (a.BombZone != null && baseDef.Element != el) a.BombZone = Zone(a.BombZone.Radius, a.BombZone.Dur, el, a.BombZone.Status, a.BombZone.Mul);
        switch (d.Id)
        {
            case "frost_pallbearer": d.OnHit = St.Knock(6f); d.OnHit2 = St.Chill(0.3f); break;
            case "frost_snuffer": d.OnHit = St.Weaken(0.25f, 4f); d.OnHit2 = St.Chill(0.2f); break;
            case "sand_snuffer": d.OnHit = St.Weaken(0.25f, 4f); d.OnHit2 = null; break;
            case "frost_mourner": d.OnHit = St.Chill(0.4f); d.OnHit2 = new StatusApply(StatusId.Fear, 0.8f, 0, 0.2f); break;
            case "mire_mourner": d.OnHit = St.Slow(0.3f, 2f); d.OnHit2 = new StatusApply(StatusId.Fear, 0.8f, 0, 0.2f); break;
            case "mire_wraith": d.OnHit = St.Slow(0.3f, 2f); d.OnHit2 = null; break;
            case "mire_chained": d.OnHit = St.Poison(0.2f, 3f); d.OnHit2 = null; a.HookStatus = St.Slow(0.3f, 2f); break;
            case "mire_wailer": d.OnHit = null; d.OnHit2 = null; a.CallWail = St.Fear(1f); break;
            case "sand_bloated": d.OnHit = St.Weaken(0.3f, 4f); d.OnHit2 = null; break;
            // bog-oak does not burn
            case "mire_stump": d.Traits &= ~MonTrait.Flammable; d.OnHit = St.Slow(0.2f, 2f); d.OnHit2 = null; break;
            case "mire_walker": d.Traits &= ~MonTrait.Flammable; d.OnHit = St.Slow(0.3f, 2f); d.OnHit2 = St.Poison(0.2f, 3f); break;
            case "mire_hangtree": d.Traits &= ~MonTrait.Flammable; d.OnHit = St.Knock(6f); d.OnHit2 = null; a.HookStatus = St.Root(1f); break;
            case "mire_root": d.Traits &= ~MonTrait.Flammable; d.OnHit = St.Poison(0.2f, 3f); d.OnHit2 = null; a.EmergeStatus = St.Root(1.2f); break;
        }
    }

    /// <summary>Things that are spawned by behaviours but are not bestiary types: the cocoon-mother's egg sac.</summary>
    public static IEnumerable<MonsterDef> HelperDefs(Dictionary<string, MonsterDef> by)
    {
        yield return new MonsterDef
        {
            Id = "shroud_eggsac", Name = "卵囊", NameEn = "Egg-sac", Model = "prim_eggsac", Family = "shroud", Sfx = "stalker", Role = "prop", AI = MonAI.Turret,
            Hp = 0.6f, Dmg = 0f, Xp = 0f, Speed = 0f, Range = 0f, Radius = 0.55f, Poise = 99f, Element = Element.Poison, Rig = "F", BodyPlan = "swarm / turret / siege",
            Slot = PackSlot.Placed,
        };
    }
}
