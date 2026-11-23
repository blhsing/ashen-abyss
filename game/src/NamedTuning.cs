using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Measured tuning: life of each named encounter in "ordinary monster lives of its level", set from --suite=named runs
/// (bot rotation, emberblade, rare gear of the right level; pinnacle at world tier 2 with paragon 50) so that the kill
/// lands near the middle of its window — rare 20–45 s, stronghold / dungeon 45–90 s, world 2–4 min, pinnacle 2–5 min (end-game: tuned for strong, well-geared players, not for the AI pilot).
/// Mechanics that hide or shield the monster are paid for here, not by weakening the mechanic.
/// </summary>
public static partial class NamedDb
{
    private static readonly Dictionary<string, float> MeasuredHp = new()
    {
        // 2026-11-22: re-measured after the 0.4.0 weapon system (the hero hits harder), from two full runs — roster off
        // (stand-ins) and on (--bestiary; weighted, the roster ships on) and the BESTIARY agent's named:rare run. Rares keep a
        // spread: their two elite affixes roll.
        // rare elites (→ ~32 s)
        ["rare_wastes_oldhowl"] = 28f, ["rare_wastes_nightwatch"] = 20f, ["rare_wastes_ashbride"] = 12f, ["rare_wastes_gallowsfather"] = 25f,
        ["rare_frost_hamo"] = 12f, ["rare_frost_whiteshadow"] = 4.5f, ["rare_frost_lastrun"] = 15f, ["rare_frost_thousandyear"] = 10f,
        ["rare_crypt_belak"] = 6.5f, ["rare_crypt_veilwife"] = 6f, ["rare_crypt_namelessking"] = 21f, ["rare_crypt_hundredtears"] = 14f,
        ["rare_mire_olddrum"] = 16f, ["rare_mire_guidelight"] = 24f, ["rare_mire_weepingriver"] = 11f, ["rare_mire_dafu"] = 8.5f,
        ["rare_sands_ogan"] = 13f, ["rare_sands_hundredshell"] = 23f, ["rare_sands_tesa"] = 30f, ["rare_sands_thirteenth"] = 27f,
        ["rare_sanctum_firstcantor"] = 14f, ["rare_sanctum_everquiet"] = 16f, ["rare_sanctum_kolen"] = 12f, ["rare_sanctum_vesna"] = 18f,
        ["rare_abyss_foreman"] = 18f, ["rare_abyss_firstborn"] = 20f, ["rare_abyss_whitetide"] = 24f, ["rare_abyss_slagferry"] = 17f,
        // stronghold bosses (→ ~65 s)
        ["sh_wastes"] = 48f, ["sh_frost"] = 36f, ["sh_crypt"] = 27f, ["sh_mire"] = 40f, ["sh_sands"] = 38f, ["sh_sanctum"] = 52f, ["sh_abyss"] = 34f,
        // side-dungeon bosses (→ ~65 s)
        ["db_wastes_d1"] = 45f, ["db_wastes_d2"] = 7f, ["db_frost_d1"] = 30f, ["db_frost_d2"] = 50f, ["db_crypt_d1"] = 20f, ["db_crypt_d2"] = 42f,
        ["db_mire_d1"] = 40f, ["db_mire_d2"] = 46f, ["db_sands_d1"] = 33f, ["db_sands_d2"] = 55f, ["db_sanctum_d1"] = 30f, ["db_sanctum_d2"] = 40f,
        ["db_abyss_d1"] = 38f, ["db_abyss_d2"] = 45f,
        // world bosses (→ ~3 min)
        ["wb_barrowbearer"] = 85f, ["wb_firstbearer"] = 110f, ["wb_daylessdisc"] = 60f, ["wb_breathofsilence"] = 55f,
        // pinnacle (→ ~3.5 min); run 1 was at level 57 with 1.2× damage and no paragon — level, damage and hero fixed since
        // end-game content for strong, well-geared players (the lead, 2026-11-22): measured with the god-mode suite bot,
        // whose damage stands for a strong build; pin_1 and pin_4 keep the formula (55.7 / 67.2). pin_7 36 → 50: it ran FAST (118 s)
        ["pin_2_rimelord"] = 28f, ["pin_3_bishop"] = 50f, ["pin_5_sand_tyrant"] = 60f, ["pin_6_grand_hush"] = 50f, ["pin_7_firstkeeper"] = 50f, ["pin_8_osaros"] = 55f,
    };

    private static readonly Dictionary<NamedKind, float> MeasuredDmg = new() { [NamedKind.World] = 1.6f };

    static partial void Tune()
    {
        foreach (var n in All)
        {
            if (MeasuredHp.TryGetValue(n.Id, out float hp)) n.Hp = hp;
            if (MeasuredDmg.TryGetValue(n.Kind, out float dmg)) n.Dmg = dmg;
        }
    }
}
