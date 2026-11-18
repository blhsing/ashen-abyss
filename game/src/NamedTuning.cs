using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// Measured tuning: life of each named encounter in "ordinary monster lives of its level", set from --suite=named runs
/// (bot rotation, emberblade, rare gear of the right level; pinnacle at world tier 2 with paragon 50) so that the kill
/// lands near the middle of its window — rare 20–45 s, stronghold / dungeon 45–90 s, world 2–4 min, pinnacle 2–5 min.
/// Mechanics that hide or shield the monster are paid for here, not by weakening the mechanic.
/// </summary>
public static partial class NamedDb
{
    private static readonly Dictionary<string, float> MeasuredHp = new()
    {
        // rare elites (run 1 → target ~32 s)
        ["rare_wastes_oldhowl"] = 18f, ["rare_wastes_nightwatch"] = 17.5f, ["rare_wastes_ashbride"] = 9f, ["rare_wastes_gallowsfather"] = 25f,
        ["rare_frost_hamo"] = 4.5f, ["rare_frost_whiteshadow"] = 5.5f, ["rare_frost_lastrun"] = 10.5f, ["rare_frost_thousandyear"] = 10f,
        ["rare_crypt_belak"] = 6.5f, ["rare_crypt_veilwife"] = 7f, ["rare_crypt_namelessking"] = 21f, ["rare_crypt_hundredtears"] = 7.5f,
        ["rare_mire_olddrum"] = 20f, ["rare_mire_guidelight"] = 24f, ["rare_mire_weepingriver"] = 6f, ["rare_mire_dafu"] = 12f,
        ["rare_sands_ogan"] = 11f, ["rare_sands_hundredshell"] = 20f, ["rare_sands_tesa"] = 21f, ["rare_sands_thirteenth"] = 21f,
        ["rare_sanctum_firstcantor"] = 10f, ["rare_sanctum_everquiet"] = 18f, ["rare_sanctum_kolen"] = 10.5f, ["rare_sanctum_vesna"] = 12f,
        ["rare_abyss_foreman"] = 13.5f, ["rare_abyss_firstborn"] = 14f, ["rare_abyss_whitetide"] = 10f, ["rare_abyss_slagferry"] = 12f,
        // stronghold bosses (→ ~65 s)
        ["sh_wastes"] = 48f, ["sh_frost"] = 28f, ["sh_crypt"] = 18f, ["sh_mire"] = 36f, ["sh_sands"] = 31.5f, ["sh_sanctum"] = 35.5f, ["sh_abyss"] = 34f,
        // side-dungeon bosses (→ ~65 s)
        ["db_wastes_d1"] = 21f, ["db_wastes_d2"] = 5f, ["db_frost_d1"] = 23f, ["db_frost_d2"] = 39f, ["db_crypt_d1"] = 8f, ["db_crypt_d2"] = 30.5f,
        ["db_mire_d1"] = 40f, ["db_mire_d2"] = 28.5f, ["db_sands_d1"] = 25f, ["db_sands_d2"] = 55f, ["db_sanctum_d1"] = 30f, ["db_sanctum_d2"] = 33f,
        ["db_abyss_d1"] = 36.5f, ["db_abyss_d2"] = 26f,
        // world bosses (→ ~3 min)
        ["wb_barrowbearer"] = 88f, ["wb_firstbearer"] = 80f, ["wb_daylessdisc"] = 60f, ["wb_breathofsilence"] = 51f,
        // pinnacle (→ ~3.5 min); run 1 was at level 57 with 1.2× damage and no paragon — level, damage and hero fixed since
        ["pin_2_rimelord"] = 28f, ["pin_3_bishop"] = 50f, ["pin_5_sand_tyrant"] = 60f, ["pin_6_grand_hush"] = 50f, ["pin_7_firstkeeper"] = 36f, ["pin_8_osaros"] = 55f,
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
