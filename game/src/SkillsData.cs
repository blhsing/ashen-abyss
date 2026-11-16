using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>Passive node in the skill tree. Minor passives have 3 ranks of stats; key passives are exclusive and hook-driven.</summary>
public sealed class PassiveDef
{
    public string Id, Name, Desc;
    public ClassId Class;
    public int UnlockLevel;
    public bool Key;
    public (Stat s, float v)[] PerRank = Array.Empty<(Stat, float)>();
    public int MaxRank => Key ? 1 : 3;
}

public static class SkillDb
{
    public static readonly List<SkillDef> All = new();
    public static readonly List<PassiveDef> Passives = new();
    public static readonly List<ClassSpec> Specs = new();
    private static readonly Dictionary<string, SkillDef> ById = new();
    public static readonly int[] ClusterLevels = { 1, 2, 4, 7, 10, 14, 20 };
    public const int SpecLevel = 15, KeyPassiveLevel = 25;

    public static SkillDef Get(string id) => id != null && ById.TryGetValue(id, out var s) ? s : null;
    public static IEnumerable<SkillDef> Of(ClassId c) => All.Where(s => s.Class == c);
    public static IEnumerable<PassiveDef> PassivesOf(ClassId c) => Passives.Where(p => p.Class == c);
    public static IEnumerable<ClassSpec> SpecsOf(ClassId c) => Specs.Where(s => s.Class == c);
    public static ClassSpec Spec(string id) => Specs.FirstOrDefault(s => s.Id == id);
    public static PassiveDef Passive(string id) => Passives.FirstOrDefault(p => p.Id == id);
    public static string DefaultBasic(ClassId c) => Of(c).First(s => s.Cat == SkillCat.Basic).Id;

    private static ClassId _c;
    private static SkillDef N(string id, SkillCat cat, int lvl, string name, string icon, string desc)
    {
        var s = new SkillDef { Id = id, Class = _c, Cat = cat, UnlockLevel = lvl, Name = name, Icon = icon, Desc = desc };
        All.Add(s); ById[id] = s;
        return s;
    }
    private static SkillUp U(string name, string desc, Action<SkillDef> mod) => new() { Name = name, Desc = desc, Mod = mod };
    private static void P(string id, int lvl, string name, params (Stat, float)[] perRank) =>
        Passives.Add(new PassiveDef { Id = id, Class = _c, UnlockLevel = lvl, Name = name, PerRank = perRank,
            Desc = string.Join("、", perRank.Select(t => StatInfo.Format(t.Item1, t.Item2))) + "（每級）" });
    private static void K(string id, string name, string desc) =>
        Passives.Add(new PassiveDef { Id = id, Class = _c, UnlockLevel = KeyPassiveLevel, Key = true, Name = name, Desc = desc });
    private static void Sp(string id, string name, string desc, Action<SkillDef> mod, params (Stat, float)[] stats) =>
        Specs.Add(new ClassSpec { Id = id, Class = _c, Name = name, Desc = desc, Mod = mod, Stats = stats });
    private static void Rep(SkillDef s, StatusId id, float dur) { s.OnHit.RemoveAll(x => x.Id == id); s.OnHit.Add(new StatusApply(id, dur)); }
    private static BuffDef Flagged(SkillDef s, string suffix, Action<BuffDef> f)
    {
        var b = new BuffDef { Id = s.Id + "_" + suffix, Name = s.Name };
        f(b); s.Buffs.Add(b); return b;
    }

    static SkillDb()
    {
        const string SW = "swing_light", SH = "swing_heavy";
        string[] swords = { "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C" };

        // ══════════════════ 燼刃衛 Emberblade ══════════════════
        _c = ClassId.Emberblade;
        N("eb_slash", SkillCat.Basic, 1, "燼斬", "broadsword", "揮動巨劍斬擊前方敵人，造成 {0}% 武器傷害。產生 12 點怒焰。")
            .Melee(0.52f, 3.0f).Res(-12).Cast("Sword_Regular_A", 1.35f, 0.22f, 0.42f, SW, "hit_flesh").Alt(swords).With(s => s.Lucky = 0.5f)
            .Ups_(U("三重燼斬", "每第三擊造成 60% 額外傷害，並使敵人易傷 2 秒。", s => { s.Nth = 3; s.NthMod = n => { n.Coef *= 1.6f; n.OnHit.Add(St.Vuln(2)); }; }),
                  U("鐵骨", "施放後獲得 3% 傷害減免，持續 4 秒，最多疊加 4 層。", s => s.B_(Stat.DmgReduction, 3, 4, "鐵骨", 4)),
                  U("熾怒", "怒焰高於 50 時，燼斬的攻擊速度提高 25%。", s => s.Flag("fast_above50")));
        N("eb_cleave", SkillCat.Core, 2, "裂地斬", "sword-slice", "重劈地面，向前方釋放一道烈焰衝擊波，造成 {0}% 武器傷害的火焰傷害並點燃敵人。")
            .Wave(1.65f, 9f, 2.4f).El(Element.Fire).Res(30).Cast("Sword_Attack", 1.25f, 0.42f, 0.7f, SH, "fire_impact").Hit(St.Burn(0.4f, 3))
            .Ups_(U("焦土", "衝擊波經過之處留下燃燒的地面，持續 3 秒，每秒造成 40% 武器傷害。", s => s.Leave = new ZoneSpec { Coef = 0.2f, Dur = 3, Radius = 2.4f }),
                  U("崩裂", "衝擊波寬度提高 35%，並使命中的敵人暈眩 1 秒。", s => { s.Radius *= 1.35f; s.OnHit.Add(St.Stun(1)); }),
                  U("引燃", "對燃燒中的敵人爆擊機率提高 30%。", s => s.CritVs[StatusId.Burn] = 30));
        N("eb_whirl", SkillCat.Core, 5, "旋風斬", "spinning-sword", "持續引導：化為旋轉的劍刃風暴，每 0.3 秒對周圍敵人造成 {0}% 武器傷害。每秒消耗 14 點怒焰，施放時可以移動。")
            .Chan(0.36f, 3.4f).Res(14).Cast("Sword_Regular_B", 1f, 0f, 0f, "whirlwind_loop", "hit_flesh").With(s => s.Lucky = 0.2f)
            .Ups_(U("漸強", "每持續引導 1 秒，旋風斬傷害提高 10%，最多 50%。", s => s.Flag("ramp")),
                  U("渦流", "旋風斬會將附近的敵人緩緩拉向你。", s => s.OnHit.Add(St.Pull(3))),
                  U("餘焰", "結束引導時釋放火焰新星，造成 130% 武器傷害。", s => { s.EndBurst = 1.3f; s.EndBurstRadius = 5; }));
        N("eb_roar", SkillCat.Defensive, 4, "戰吼", "shouting", "發出震懾戰吼：獲得 30% 傷害減免與 20% 移動速度，持續 6 秒，並產生 30 點怒焰。")
            .Buff(6).Res(-30, 18).Cast("Idle_Rail_Call", 1.6f, 0.25f, 0.45f, "war_cry").B_(Stat.DmgReduction, 30, 0).B_(Stat.MoveSpeed, 20, 0).With(s => s.CastBurstRadius = 8)
            .Ups_(U("震懾", "附近的敵人被緩速 40%，持續 4 秒。", s => s.OnHit.Add(St.Slow(0.4f, 4))),
                  U("無可阻擋", "戰吼持續期間免疫控制效果。", s => Flagged(s, "u", b => b.Unstoppable = true)),
                  U("破膽", "使附近的敵人易傷 4 秒。", s => s.OnHit.Add(St.Vuln(4))));
        N("eb_leap", SkillCat.Mobility, 7, "躍擊", "leapfrog", "躍向目標位置並重擊地面，對落點周圍敵人造成 {0}% 武器傷害。")
            .Leap(1.15f, 13f, 4f).Res(0, 10).Cast("NinjaJump_Start", 1.3f, 0.5f, 0.75f, "evade", "leap_land")
            .Ups_(U("震地", "落地使敵人暈眩 1.5 秒。", s => s.OnHit.Add(St.Stun(1.5f))),
                  U("蓄怒", "落地時產生 40 點怒焰。", s => s.Cost = -40),
                  U("燼環", "冷卻時間縮短 3 秒，並在落點留下燃燒的火環。", s => { s.Cooldown -= 3; s.Leave = new ZoneSpec { Coef = 0.25f, Dur = 4, Radius = 4 }; }));
        N("eb_ironhide", SkillCat.Defensive, 10, "鐵壁", "shield", "舉劍格擋，獲得相當於生命上限 40% 的屏障，持續 6 秒。")
            .Buff(6).Res(0, 20).Cast("Sword_Block", 1.5f, 0.1f, 0.3f, "barrier_up").With(s => s.Barrier = 0.4f)
            .Ups_(U("堅守", "鐵壁持續期間額外獲得 15% 傷害減免。", s => s.B_(Stat.DmgReduction, 15, 0)),
                  U("碎壁", "鐵壁結束時爆炸，對周圍造成 180% 武器傷害。", s => { s.EndBurst = 1.8f; s.EndBurstRadius = 5; }),
                  U("固守", "鐵壁持續期間每秒恢復 3% 生命上限。", s => Flagged(s, "r", b => b.RegenPct = 3)));
        N("eb_rupture", SkillCat.Mastery, 14, "燼火重擊", "sword-clash", "蓄力重斬，對前方敵人造成 {0}% 武器傷害的火焰傷害，使其易傷並燃燒 4 秒。")
            .Melee(2.9f, 4f, 110f).El(Element.Fire).Res(0, 12).Cast("Sword_Heavy_Combo", 1.5f, 0.55f, 0.95f, SH, "hit_crit").Hit(St.Vuln(4), St.Burn(1.5f, 4)).With(s => s.Lucky = 0.6f)
            .Ups_(U("嗜戰", "以燼火重擊擊殺敵人時重置其冷卻時間。", s => s.ResetOnKill = true),
                  U("弒強", "對菁英與首領的傷害提高 40%。", s => s.EliteDmg = 0.4f),
                  U("橫掃", "攻擊範圍大幅擴大。", s => { s.Radius *= 1.5f; s.Arc = 220; }));
        N("eb_wrath", SkillCat.Ultimate, 20, "焚世之怒", "fire-dash", "引燃體內的燼火 12 秒：傷害提高 40%，攻擊速度提高 25%，所有攻擊點燃敵人。")
            .Buff(12).El(Element.Fire).Res(0, 60).Cast("Idle_Rail_Call", 1.3f, 0.3f, 0.5f, "ember_buff").B_(Stat.DmgAll, 40, 0).B_(Stat.AttackSpeed, 25, 0)
            .With(s => { Flagged(s, "f", b => b.Burn = 0.5f); s.CastBurstRadius = 7; })
            .Ups_(U("燼爆", "啟動時釋放火焰衝擊波，造成 220% 武器傷害。", s => s.CastBurst = 2.2f),
                  U("不熄", "持續期間每次擊殺使效果延長 1 秒。", s => s.Flag("extend_on_kill")),
                  U("飲火", "持續期間造成傷害的 4% 轉化為生命。", s => Flagged(s, "l", b => b.LifeSteal = 4)));
        P("eb_p1", 2, "劍術精研", (Stat.DmgCore, 5)); P("eb_p2", 4, "厚甲", (Stat.Armor, 40), (Stat.LifePct, 2));
        P("eb_p3", 10, "餘燼之血", (Stat.DmgFire, 5), (Stat.LifeOnKill, 4)); P("eb_p4", 14, "戰意", (Stat.ResourceGen, 6), (Stat.DmgOverpower, 8));
        K("kp_eb_edge", "熾刃", "怒焰高於 75 時，核心技能造成的傷害提高 30%。");
        K("kp_eb_stand", "不退", "生命每損失 10%，獲得 3% 傷害減免。");
        Sp("eb_heavy", "重劍式", "核心技能傷害 +20%，壓制傷害 +30%。", null, (Stat.DmgCore, 20), (Stat.DmgOverpower, 30));
        Sp("eb_swift", "疾劍式", "攻擊速度 +12%，怒焰產生 +20%。", null, (Stat.AttackSpeed, 12), (Stat.ResourceGen, 20));
        Sp("eb_guard", "守劍式", "傷害減免 +10%，荊棘傷害 +150。", null, (Stat.DmgReduction, 10), (Stat.Thorns, 150));

        // ══════════════════ 星術師 Astromancer ══════════════════
        _c = ClassId.Astromancer;
        N("as_spark", SkillCat.Basic, 1, "星火", "sparkles", "射出一道星火，造成 {0}% 武器傷害的閃電傷害。")
            .Proj(0.44f, 18f).El(Element.Lightning).Cast("Spell_Simple_Shoot", 1.7f, 0.18f, 0.36f, "lightning_cast", "lightning_zap").With(s => s.Lucky = 0.5f)
            .Ups_(U("躍動", "命中後彈射至另一名敵人，造成 70% 傷害。", s => s.Bounce = 1),
                  U("回流", "星火爆擊時恢復 4 點靈能。", s => s.GainOnCrit = 4),
                  U("聚光", "每第三發星火化為穿透所有敵人的光束。", s => { s.Nth = 3; s.NthMod = n => { n.Pierce = -1; n.Speed = 60; }; }));
        N("as_fireball", SkillCat.Core, 2, "炎爆彈", "fireball", "擲出火球，命中後爆炸，對範圍內敵人造成 {0}% 武器傷害的火焰傷害並使其燃燒。")
            .Proj(1.4f, 18f, 1, 0, 22f).El(Element.Fire).Res(28).Cast("Spell_Simple_Shoot", 1.3f, 0.24f, 0.48f, "fire_cast", "fire_impact").Hit(St.Burn(0.6f, 4)).With(s => { s.Explode = 3.2f; s.Lucky = 0.4f; })
            .Ups_(U("烈爆", "爆炸半徑擴大 35%。", s => s.Explode *= 1.35f),
                  U("裂變", "爆炸後分裂出 3 顆小火球，各造成 50% 傷害。", s => s.SplitCount = 3),
                  U("灼痕", "燃燒傷害提高 100%。", s => { s.OnHit.RemoveAll(x => x.Id == StatusId.Burn); s.OnHit.Add(St.Burn(1.2f, 4)); }));
        N("as_frostlance", SkillCat.Core, 5, "霜槍", "ice-spear", "擲出穿透敵人的冰槍，造成 {0}% 武器傷害的冰霜傷害並使其冰緩。")
            .Proj(1.15f, 20f, 1, -1, 34f).El(Element.Frost).Res(22).Cast("Spell_Simple_Shoot", 1.4f, 0.22f, 0.44f, "frost_cast", "frost_impact").Hit(St.Chill(0.35f))
            .Ups_(U("碎冰", "對冰緩或凍結的敵人爆擊機率提高 25%。", s => { s.CritVs[StatusId.Chill] = 25; s.CritVs[StatusId.Freeze] = 25; }),
                  U("冰棘扇", "一次擲出 3 支冰槍，呈扇形散開。", s => { s.Count = 3; s.Spread = 14; }),
                  U("凍結", "冰槍會凍結命中的敵人 1.5 秒。", s => s.OnHit.Add(St.Freeze(1.5f))));
        N("as_chain", SkillCat.Mastery, 10, "連鎖閃電", "chain-lightning", "釋放在敵人之間跳躍的閃電，最多命中 5 個目標，每次造成 {0}% 武器傷害。")
            .Chain(0.78f, 16f, 5).El(Element.Lightning).Res(30).Cast("Spell_Simple_Shoot", 1.3f, 0.24f, 0.46f, "lightning_zap", "lightning_zap").With(s => s.Lucky = 0.25f)
            .Ups_(U("擴散", "額外跳躍 2 次。", s => s.Count += 2),
                  U("蓄電", "每次跳躍的傷害提高 15%。", s => s.ChainRamp = 0.15f),
                  U("麻痺", "命中的敵人暈眩 0.75 秒。", s => s.OnHit.Add(St.Stun(0.75f))));
        N("as_blink", SkillCat.Mobility, 7, "星躍", "teleport", "瞬間移動至目標位置，並在原地留下閃電新星，造成 {0}% 武器傷害。")
            .Blink(0.85f, 13f, 4f).El(Element.Lightning).Res(0, 9).Cast("Spell_Simple_Shoot", 2f, 0.08f, 0.2f, "teleport", "lightning_zap")
            .Ups_(U("星幕", "抵達時獲得相當於生命上限 20% 的屏障，持續 4 秒。", s => { s.Barrier = 0.2f; s.BarrierDur = 4; }),
                  U("雙躍", "星躍可儲存 2 次充能。", s => s.Charges = 2),
                  U("流光", "冷卻時間縮短 2 秒，抵達後移動速度提高 30%，持續 3 秒。", s => { s.Cooldown -= 2; s.B_(Stat.MoveSpeed, 30, 3); }));
        N("as_frostnova", SkillCat.Defensive, 4, "霜環", "frozen-ring", "釋放寒霜之環，凍結周圍敵人 2.5 秒並造成 {0}% 武器傷害的冰霜傷害。")
            .Nova(0.55f, 6.5f).El(Element.Frost).Res(0, 16).Cast("Spell_Simple_Shoot", 1.8f, 0.15f, 0.36f, "nova", "frost_shatter").Hit(St.Freeze(2.5f))
            .Ups_(U("脆化", "被凍結的敵人陷入易傷，持續 4 秒。", s => s.OnHit.Add(St.Vuln(4))),
                  U("寒潮", "冷卻時間縮短 4 秒。", s => s.Cooldown -= 4),
                  U("霜甲", "施放時獲得相當於生命上限 25% 的屏障，持續 5 秒。", s => { s.Barrier = 0.25f; s.BarrierDur = 5; }));
        N("as_meteor", SkillCat.Mastery, 14, "隕星術", "meteor-impact", "召喚隕星墜向目標位置，1 秒後對範圍內敵人造成 {0}% 武器傷害的火焰傷害，並留下燃燒的地面。")
            .Ground(3.3f, 18f, 4.2f, 1.0f).El(Element.Fire).Res(45, 3).Cast("Spell_Simple_Shoot", 1.1f, 0.3f, 0.55f, "meteor_fall", "meteor_explode").Hit(St.Burn(0.6f, 4))
            .With(s => { s.Leave = new ZoneSpec { Coef = 0.15f, Dur = 4, Radius = 4.2f }; s.Lucky = 0.5f; })
            .Ups_(U("震盪", "隕星使命中的敵人暈眩 1 秒。", s => s.OnHit.Add(St.Stun(1))),
                  U("星雨", "主隕星墜落後，周圍再墜落 3 顆小隕星，各造成 35% 傷害。", s => s.ExtraStrikes = 3),
                  U("熔池", "燃燒的地面持續時間延長 3 秒，並使敵人緩速 30%。", s => { s.Leave.Dur += 3; s.Leave.OnHit.Add(St.Slow(0.3f, 1)); }));
        N("as_cataclysm", SkillCat.Ultimate, 20, "星隕天災", "burning-meteor", "撕裂天穹 8 秒：隕星不斷墜向附近的敵人，每顆造成 {0}% 武器傷害的火焰傷害。")
            .Buff(8).El(Element.Fire).Res(0, 70).Cast("Spell_Simple_Enter", 1.2f, 0.4f, 0.7f, "thunder", "meteor_explode")
            .With(s => { s.Coef = 1.55f; s.Rain = 0.35f; s.RainCoef = 1f; s.RainRadius = 3.2f; s.Lucky = 0.2f; Flagged(s, "t", b => { }); })
            .Ups_(U("無盡星能", "持續期間施放技能不消耗靈能。", s => Flagged(s, "f", b => b.FreeCast = true)),
                  U("長夜", "持續時間延長 3 秒。", s => s.Duration += 3),
                  U("霜星", "隕星改為冰霜傷害，並凍結命中的敵人 1 秒。", s => { s.Element = Element.Frost; s.OnHit.Add(St.Freeze(1)); }));
        P("as_p1", 2, "毀滅", (Stat.CritDmg, 5)); P("as_p2", 4, "元素護體", (Stat.ResAll, 3), (Stat.BarrierPct, 5));
        P("as_p3", 10, "星能湧動", (Stat.ResourceGen, 5), (Stat.MaxResource, 4)); P("as_p4", 14, "精準施法", (Stat.CritChance, 1.5f), (Stat.DmgCore, 4));
        K("kp_as_overload", "過載", "爆擊時有 15% 機率使你的下一個技能不消耗靈能。");
        K("kp_as_mastery", "元素專精", "對燃燒、冰緩或凍結的敵人造成的傷害提高 20%。");
        Sp("as_fire", "熾星", "火焰傷害 +20%，火焰技能的爆炸範圍擴大 15%。", s => { if (s.Element == Element.Fire) { s.Explode *= 1.15f; if (s.Kind == SkillKind.Ground) s.Radius *= 1.15f; } }, (Stat.DmgFire, 20));
        Sp("as_frost", "霜星", "冰霜傷害 +20%，屏障強度 +20%。", null, (Stat.DmgFrost, 20), (Stat.BarrierPct, 20));
        Sp("as_storm", "雷星", "閃電傷害 +20%，爆擊機率 +5%。", null, (Stat.DmgLightning, 20), (Stat.CritChance, 5));

        // ══════════════════ 影獵手 Hunter ══════════════════
        _c = ClassId.Hunter;
        N("hu_shot", SkillCat.Basic, 1, "連射", "crossbow", "以手弩射出弩矢，造成 {0}% 武器傷害。命中獲得 1 點連擊點（上限 3）。")
            .Proj(0.42f, 19f, 1, 0, 40f).Cast("Pistol_Shoot", 1.6f, 0.1f, 0.3f, "bolt_shoot", "bolt_impact").With(s => { s.PipGain = 1; s.Lucky = 0.5f; })
            .Ups_(U("重矢", "每第三發弩矢穿透敵人並造成 50% 額外傷害。", s => { s.Nth = 3; s.NthMod = n => { n.Pierce = -1; n.Coef *= 1.5f; }; }),
                  U("雙發", "有 20% 機率同時射出兩支弩矢。", s => s.ExtraChance = 0.2f),
                  U("絆矢", "命中使敵人緩速 25%，持續 2 秒。", s => s.OnHit.Add(St.Slow(0.25f, 2))));
        N("hu_pierce", SkillCat.Core, 2, "穿雲矢", "arrow-flights", "射出貫穿一切的重矢，造成 {0}% 武器傷害。每消耗 1 點連擊點，傷害提高 30%。")
            .Proj(1.35f, 22f, 1, -1, 46f).Res(30).Cast("Pistol_Shoot", 1.15f, 0.16f, 0.42f, "bolt_shoot", "bolt_impact").With(s => { s.PipSpend = true; s.Lucky = 0.4f; })
            .Ups_(U("趁隙", "對易傷的敵人傷害提高 30%。", s => s.DmgVs[StatusId.Vulnerable] = 0.3f),
                  U("三叉", "同時射出 3 支穿雲矢，各造成 65% 傷害。", s => { s.Count = 3; s.Coef *= 0.65f; s.Spread = 12; }),
                  U("釘刺", "擊退並暈眩命中的敵人 0.75 秒。", s => { s.OnHit.Add(St.Knock(5)); s.OnHit.Add(St.Stun(0.75f)); }));
        N("hu_flurry", SkillCat.Core, 5, "影刃亂舞", "daggers", "以短刃疾刺前方敵人 4 次，每次造成 {0}% 武器傷害的暗影傷害。每消耗 1 點連擊點，傷害提高 30%。")
            .Melee(0.42f, 3.0f, 110f, 4).El(Element.Shadow).Res(25).Cast("Sword_Regular_Combo", 1.7f, 0.15f, 0.75f, SW, "hit_flesh").With(s => { s.PipSpend = true; s.Lucky = 0.2f; })
            .Ups_(U("破綻", "命中使敵人易傷 3 秒。", s => s.OnHit.Add(St.Vuln(3))),
                  U("飲影", "每次命中恢復 1.5% 生命上限。", s => s.HealOnHit = 1.5f),
                  U("淬毒", "命中使敵人中毒，4 秒內造成 100% 武器傷害。", s => { s.Element = Element.Poison; s.OnHit.Add(St.Poison(0.25f, 4)); }));
        N("hu_dash", SkillCat.Mobility, 4, "影襲", "sprint", "化為殘影穿過敵人，對途經的敵人造成 {0}% 武器傷害的暗影傷害。可儲存 2 次充能。")
            .Dash(0.95f, 9f, 1.6f).El(Element.Shadow).Res(0, 8, 2).Cast("Sword_Dash", 1.6f, 0f, 0.32f, "evade", "shadow_impact")
            .Ups_(U("鋒芒", "施放後爆擊機率提高 20%，持續 4 秒。", s => s.B_(Stat.CritChance, 20, 4)),
                  U("迅影", "影襲可額外儲存 1 次充能。", s => s.Charges = 3),
                  U("殘影", "在起點留下一道殘影，1 秒後爆炸造成 120% 武器傷害。", s => s.OriginBurst = 1.2f));
        N("hu_smoke", SkillCat.Defensive, 7, "煙幕", "smoke-bomb", "擲下煙幕彈：使周圍敵人暈眩 2 秒，你進入潛行 3 秒並提高 30% 移動速度。")
            .Nova(0f, 5.5f).Res(0, 18).Cast("OverhandThrow", 2f, 0.15f, 0.3f, "shadow_cast").Hit(St.Stun(2)).B_(Stat.MoveSpeed, 30, 3).With(s => Flagged(s, "s", b => { b.Stealth = true; b.Dur = 3; }))
            .Ups_(U("伏擊", "潛行狀態下的第一次攻擊必定爆擊。", s => s.Buffs.Find(b => b.Stealth).Ambush = true),
                  U("熟練", "冷卻時間縮短 5 秒。", s => s.Cooldown -= 5),
                  U("毒霧", "煙幕持續 4 秒，每秒對其中的敵人造成 60% 武器傷害的毒素傷害。", s => s.Leave = new ZoneSpec { Coef = 0.3f, Dur = 4, Radius = 5.5f, El = Element.Poison }));
        N("hu_trap", SkillCat.Mastery, 10, "棘刺陷阱", "wolf-trap", "擲出陷阱，敵人靠近時引爆，造成 {0}% 武器傷害並定身 2 秒。最多同時存在 2 個。")
            .Trap(2.1f, 14f, 4f).Res(20, 6).Cast("OverhandThrow", 1.8f, 0.2f, 0.36f, "trap_arm", "trap_trigger").Hit(St.Root(2)).With(s => { s.SummonMax = 2; s.Lucky = 0.4f; })
            .Ups_(U("布置", "可同時存在的陷阱數量 +1。", s => s.SummonMax = 3),
                  U("寒鐵", "改為冰霜傷害，並凍結敵人 2 秒。", s => { s.Element = Element.Frost; s.OnHit.Clear(); s.OnHit.Add(St.Freeze(2)); }),
                  U("毒囊", "引爆後留下毒霧 6 秒，每秒造成 45% 武器傷害。", s => s.Leave = new ZoneSpec { Coef = 0.225f, Dur = 6, Radius = 4, El = Element.Poison }));
        N("hu_rain", SkillCat.Mastery, 14, "箭雨", "arrow-cluster", "向目標區域傾瀉弩矢 3 秒，每 0.375 秒造成 {0}% 武器傷害並緩速敵人。")
            .Zone(0.48f, 17f, 5f, 3f, 0.375f).Res(40, 10).Cast("Pistol_Shoot", 1f, 0.2f, 0.45f, "bolt_shoot", "bolt_impact").Hit(St.Slow(0.3f, 1)).With(s => s.Lucky = 0.15f)
            .Ups_(U("密雨", "作用範圍擴大 25%。", s => s.Radius *= 1.25f),
                  U("綿延", "持續時間延長 2 秒。", s => s.Duration += 2),
                  U("終矢", "結束時落下一支爆裂矢，造成 160% 武器傷害。", s => { s.EndBurst = 1.6f; s.EndBurstRadius = 5; }));
        N("hu_mark", SkillCat.Ultimate, 20, "死兆", "death-zone", "進入狩獵狀態 10 秒：攻擊速度提高 30%，投射物技能額外射出 2 支弩矢，命中的敵人陷入易傷。")
            .Buff(10).Res(0, 60).Cast("Idle_Rail_Call", 1.5f, 0.2f, 0.4f, "shadow_cast").B_(Stat.AttackSpeed, 30, 0).With(s => Flagged(s, "h", b => { b.ExtraProj = 2; b.VulnDur = 3; }))
            .Ups_(U("整備", "啟動時重置其他技能的冷卻時間。", s => s.ResetAllCds = true),
                  U("獵殺", "持續期間每次擊殺恢復 15 點精力。", s => s.Buffs.Find(b => b.ExtraProj > 0).GainOnKill = 15),
                  U("影爆", "持續期間每次爆擊觸發影爆，對周圍造成 60% 武器傷害。", s => s.Buffs.Find(b => b.ExtraProj > 0).CritBurst = 0.6f));
        P("hu_p1", 2, "要害打擊", (Stat.CritChance, 1.5f)); P("hu_p2", 4, "輕靈", (Stat.Dodge, 1.5f), (Stat.MoveSpeed, 2));
        P("hu_p3", 10, "淬鍊", (Stat.DmgVuln, 6), (Stat.DmgPoison, 5)); P("hu_p4", 14, "獵人耐力", (Stat.ResourceGen, 5), (Stat.MaxResource, 4));
        K("kp_hu_finisher", "致命連擊", "一次消耗 3 點以上連擊點的技能必定爆擊。");
        K("kp_hu_skirmish", "游擊", "閃避後 4 秒內造成的傷害提高 20%。");
        Sp("hu_combo", "連擊", "核心技能傷害 +20%，連擊點上限 +1。", null, (Stat.DmgCore, 20));
        Sp("hu_ambush", "伏擊", "爆擊傷害 +30%，移動速度 +6%。", null, (Stat.CritDmg, 30), (Stat.MoveSpeed, 6));
        Sp("hu_venom", "毒殺", "毒素與持續傷害 +25%，你的所有攻擊使敵人中毒。", s => { if (s.Coef > 0) s.OnHit.Add(St.Poison(0.2f, 4)); }, (Stat.DmgPoison, 25), (Stat.DmgDot, 25));

        // ══════════════════ 喚魂師 Soulbinder ══════════════════
        _c = ClassId.Soulbinder;
        N("sb_bolt", SkillCat.Basic, 1, "蝕魂彈", "bolt-spell-cast", "射出蝕魂之彈，造成 {0}% 武器傷害的暗影傷害。產生 9 點魂能。")
            .Proj(0.44f, 18f).El(Element.Shadow).Res(-9).Cast("Spell_Simple_Shoot", 1.6f, 0.18f, 0.38f, "shadow_cast", "shadow_impact").With(s => s.Lucky = 0.5f)
            .Ups_(U("剝魂", "命中有 25% 機率從敵人身上剝離一縷殘魂。", s => s.RemnantChance = 0.25f),
                  U("透體", "蝕魂彈可穿透一名敵人。", s => s.Pierce = 1),
                  U("標記", "命中的敵人受到僕從的傷害提高 25%，持續 4 秒。", s => s.OnHit.Add(St.Mark(4))));
        N("sb_raise", SkillCat.Core, 2, "喚起骸骨", "raise-skeleton", "喚起一名骸骨戰士為你作戰（上限 4 名），其攻擊造成 {0}% 武器傷害。若已達上限，則治癒所有僕從並使其傷害提高 30%，持續 6 秒。")
            .Sum("minion_skeleton", 4, 0, 0.3f, 10f).Res(25).Cast("Spell_Simple_Enter", 1.6f, 0.25f, 0.45f, "summon_raise")
            .Ups_(U("擴編", "骸骨僕從上限 +1。", s => s.SummonMax += 1),
                  U("擲矛手", "每第二名喚起的骸骨為遠程擲矛手。", s => s.Flag("thrower_alt")),
                  U("堅骨", "僕從生命提高 60%，並會嘲諷攻擊的敵人。", s => { s.MinionHpMul = 1.6f; s.Flag("taunt"); }));
        N("sb_spear", SkillCat.Core, 5, "骨矛", "bone-knife", "擲出貫穿敵人的骨矛，造成 {0}% 武器傷害。")
            .Proj(1.55f, 21f, 1, -1, 40f).Res(28).Cast("OverhandThrow", 1.6f, 0.22f, 0.45f, "knife_throw", "hit_bone").With(s => s.Lucky = 0.4f)
            .Ups_(U("碎骨", "命中首個目標後碎裂成 3 枚骨片飛散，各造成 40% 傷害。", s => { s.SplitCount = 3; s.SplitMul = 0.4f; }),
                  U("弒強", "對菁英與首領的傷害提高 40%。", s => s.EliteDmg = 0.4f),
                  U("收割", "以骨矛擊殺的敵人必定留下殘魂。", s => s.RemnantOnKill = true));
        N("sb_blight", SkillCat.Mastery, 10, "凋零", "poison-gas", "擲出腐蝕之球，在目標位置化為凋零之池 5 秒，每秒造成 {0}% 武器傷害的暗影傷害並緩速 30%。")
            .Zone(0.55f, 16f, 4.2f, 5f, 0.5f).El(Element.Shadow).Res(26).Cast("Spell_Simple_Shoot", 1.3f, 0.24f, 0.46f, "shadow_cast", "poison_splash").Hit(St.Slow(0.3f, 1)).With(s => { s.Lucky = 0.12f; s.Flag("per_second"); })
            .Ups_(U("衰朽", "凋零之池中的敵人陷入易傷。", s => s.OnHit.Add(St.Vuln(1))),
                  U("蔓延", "持續時間延長 3 秒。", s => s.Duration += 3),
                  U("亡者之地", "你的僕從在凋零之池中攻擊速度提高 30%。", s => s.Flag("minion_haste")));
        N("sb_explode", SkillCat.Mastery, 14, "魂爆", "explosion-rays", "引爆目標位置附近所有的殘魂，每縷殘魂對周圍造成 {0}% 武器傷害的暗影傷害。沒有殘魂時，在目標位置引發一次較弱的爆炸。")
            .Ground(1.9f, 16f, 3.6f, 0f).El(Element.Shadow).Res(15, 1).Cast("Spell_Simple_Shoot", 1.8f, 0.15f, 0.34f, "shadow_cast", "shadow_impact").Flag("remnants")
            .Ups_(U("擴爆", "爆炸半徑擴大 30%。", s => s.Radius *= 1.3f),
                  U("攝魂", "每次爆炸恢復 3% 生命上限。", s => s.HealPct = 3),
                  U("餘毒", "每次爆炸留下一小片凋零之池，持續 3 秒。", s => s.Leave = new ZoneSpec { Coef = 0.2f, Dur = 3, Radius = 3, El = Element.Shadow }));
        N("sb_veil", SkillCat.Defensive, 4, "幽魂帷幕", "ghost", "化為幽魂 3 秒：免疫一切傷害，移動速度提高 40%，並可穿過敵人，每 0.3 秒對身邊敵人造成 {0}% 武器傷害的暗影傷害。")
            .Buff(3).El(Element.Shadow).Res(0, 18).Cast("Spell_Simple_Enter", 2.2f, 0.05f, 0.15f, "teleport").B_(Stat.MoveSpeed, 40, 0)
            .With(s => { s.Coef = 0.3f; s.PulseCoef = 1f; s.PulseRadius = 2.2f; Flagged(s, "i", b => b.Immune = true); })
            .Ups_(U("歸魂", "結束時恢復 20% 生命上限。", s => s.EndHeal = 20),
                  U("薄幕", "冷卻時間縮短 4 秒。", s => s.Cooldown -= 4),
                  U("牽魂", "啟動時在周圍產生 4 縷殘魂。", s => s.SpawnRemnants = 4));
        N("sb_curse", SkillCat.Mobility, 7, "衰弱詛咒", "cursed-star", "詛咒目標區域的敵人 8 秒：其造成的傷害降低 25%、緩速 30%，並陷入易傷。")
            .Ground(0f, 16f, 6f, 0.1f).El(Element.Shadow).Res(15, 12).Cast("Spell_Simple_Shoot", 1.6f, 0.18f, 0.38f, "shadow_cast").Hit(St.Weaken(0.25f, 8), St.Slow(0.3f, 8), St.Vuln(8))
            .Ups_(U("咒爆", "被詛咒的敵人死亡時爆炸，對周圍造成 90% 武器傷害。", s => s.OnHit.Add(St.Hex(8, 0.9f))),
                  U("廣咒", "作用範圍擴大 40%。", s => s.Radius *= 1.4f),
                  U("輕咒", "冷卻時間縮短 4 秒，且不再消耗魂能。", s => { s.Cooldown -= 4; s.Cost = 0; }));
        N("sb_colossus", SkillCat.Ultimate, 20, "骸骨巨像", "giant", "喚起一尊骸骨巨像為你作戰 20 秒，其重擊對範圍內敵人造成 {0}% 武器傷害。")
            .Sum("minion_colossus", 1, 20, 1.6f, 8f).Res(0, 75).Cast("Spell_Simple_Enter", 1.1f, 0.45f, 0.75f, "summon_raise")
            .Ups_(U("號令", "召喚時治癒所有僕從，並使其傷害提高 50%，持續 10 秒。", s => { s.Flag("rally"); s.B_(Stat.MinionDmg, 50, 10); }),
                  U("長存", "巨像的持續時間延長 10 秒。", s => s.SummonDur += 10),
                  U("崩塌", "巨像消逝時崩塌，對周圍造成 500% 武器傷害。", s => { s.EndBurst = 5f; s.EndBurstRadius = 6; }));
        P("sb_p1", 2, "亡者之握", (Stat.MinionDmg, 6)); P("sb_p2", 4, "幽冥護體", (Stat.LifePct, 2), (Stat.ResShadow, 5));
        P("sb_p3", 10, "蝕魂", (Stat.DmgShadow, 5), (Stat.DmgDot, 5)); P("sb_p4", 14, "聚魂", (Stat.MaxResource, 4), (Stat.LifeOnKill, 5));
        K("kp_sb_chorus", "群魂共振", "每名存活的僕從使你造成的傷害提高 4%。");
        K("kp_sb_alone", "孤魂", "沒有任何僕從時，你造成的傷害提高 30%。");
        Sp("sb_legion", "骸軍", "僕從傷害 +30%，骸骨僕從上限 +1。", s => { if (s.Id == "sb_raise") s.SummonMax += 1; }, (Stat.MinionDmg, 30));
        Sp("sb_offering", "獻祭", "暗影傷害 +25%，爆擊機率 +5%。", null, (Stat.DmgShadow, 25), (Stat.CritChance, 5));
        Sp("sb_blood", "血魂", "生命上限 +12%，擊殺恢復生命 +30，受到治療效果 +15%。", null, (Stat.LifePct, 12), (Stat.LifeOnKill, 30), (Stat.Healing, 15));

        // ══════════════════ 荒靈行者 Wildsoul ══════════════════  Form: 1 bear, 2 wolf, 3 human
        _c = ClassId.Wildsoul;
        N("ws_strike", SkillCat.Basic, 1, "荒擊", "wood-club", "以圖騰戰棍揮擊前方敵人，造成 {0}% 武器傷害。產生 10 點靈息。")
            .Melee(0.5f, 3.0f).Res(-10).Cast("Sword_Regular_A", 1.3f, 0.22f, 0.42f, SW, "hit_flesh").Alt(swords).With(s => { s.Form = 3; s.Lucky = 0.5f; })
            .Ups_(U("引雷", "每第三擊引動落雷：傷害提高 70%、轉為閃電傷害並暈眩 0.5 秒。", s => { s.Nth = 3; s.NthMod = n => { n.Coef *= 1.7f; n.Element = Element.Lightning; n.OnHit.Add(St.Stun(0.5f)); }; }),
                  U("吐納", "荒擊額外產生 4 點靈息。", s => s.Cost -= 4),
                  U("纏根", "命中使敵人緩速 30%，持續 2 秒。", s => s.OnHit.Add(St.Slow(0.3f, 2))));
        N("ws_maul", SkillCat.Core, 2, "熊靈重擊", "bear-head", "化為熊靈形，以巨掌橫掃前方，造成 {0}% 武器傷害。熊靈形下獲得 20% 傷害減免。")
            .Melee(1.55f, 3.6f, 150f).Res(28).Cast("Melee_Hook", 1.2f, 0.34f, 0.6f, "beast_claw", "hit_flesh").With(s => { s.Form = 1; s.Lucky = 0.4f; })
            .Ups_(U("厚皮", "每命中一名敵人，獲得相當於生命上限 3% 的屏障。", s => s.BarrierOnHit = 0.03f),
                  U("震掌", "命中使敵人暈眩 1 秒。", s => s.OnHit.Add(St.Stun(1))),
                  U("掃蕩", "攻擊範圍擴大 30%，並擊退敵人。", s => { s.Radius *= 1.3f; s.OnHit.Add(St.Knock(6)); }));
        N("ws_rend", SkillCat.Core, 5, "狼靈撕裂", "wolf-head", "化為狼靈形，疾速撕咬前方敵人 3 次，每次造成 {0}% 武器傷害並使其流血。狼靈形下移動與攻擊速度提高 15%。")
            .Melee(0.5f, 2.8f, 110f, 3).Res(22).Cast("Zombie_Scratch", 2f, 0.14f, 0.55f, "beast_claw", "hit_flesh").Hit(St.Bleed(0.27f, 4)).With(s => { s.Form = 2; s.Lucky = 0.25f; })
            .Ups_(U("嗜血", "對流血的敵人爆擊機率提高 25%。", s => s.CritVs[StatusId.Bleed] = 25),
                  U("飲血", "每次命中恢復 1.5% 生命上限。", s => s.HealOnHit = 1.5f),
                  U("狩獵本能", "施放後移動速度提高 20%，持續 3 秒。", s => s.B_(Stat.MoveSpeed, 20, 3)));
        N("ws_bark", SkillCat.Defensive, 4, "樹皮護體", "tree-branch", "以枯林之皮裹身，獲得相當於生命上限 30% 的屏障，持續 6 秒。")
            .Buff(6).Res(0, 18).Cast("Sword_Block", 1.6f, 0.1f, 0.3f, "earth_cast").With(s => { s.Barrier = 0.3f; s.CastBurstRadius = 5; })
            .Ups_(U("年輪", "持續期間每秒恢復 2% 生命上限。", s => Flagged(s, "r", b => b.RegenPct = 2)),
                  U("磐根", "持續期間免疫控制效果。", s => Flagged(s, "u", b => b.Unstoppable = true)),
                  U("崩石", "施放時震開周圍敵人，造成 80% 武器傷害。", s => { s.CastBurst = 0.8f; s.OnHit.Add(St.Knock(7)); }));
        N("ws_pounce", SkillCat.Mobility, 7, "狼躍", "paw", "化為狼靈形撲向目標位置，對落點周圍敵人造成 {0}% 武器傷害。")
            .Leap(1.0f, 12f, 3f).Res(0, 9).Cast("NinjaJump_Start", 1.5f, 0.4f, 0.6f, "evade", "beast_bite").With(s => s.Form = 2)
            .Ups_(U("撕裂傷", "落地使敵人易傷 3 秒。", s => s.OnHit.Add(St.Vuln(3))),
                  U("連躍", "狼躍可儲存 2 次充能。", s => s.Charges = 2),
                  U("獵息", "落地時產生 30 點靈息。", s => s.Cost = -30));
        N("ws_quake", SkillCat.Mastery, 10, "地裂", "earth-crack", "猛擊地面，使前方大地崩裂，造成 {0}% 武器傷害並緩速敵人 40%，持續 3 秒。")
            .Wave(1.9f, 10f, 2.4f).Res(35, 4).Cast("Sword_Attack", 1.25f, 0.42f, 0.7f, "earth_cast", "earth_impact").Hit(St.Slow(0.4f, 3)).With(s => { s.Form = 3; s.Lucky = 0.4f; })
            .Ups_(U("地湧", "裂痕末端噴發，對周圍造成 100% 武器傷害。", s => { s.EndBurst = 1f; s.EndBurstRadius = 3.5f; }),
                  U("震盪", "命中使敵人暈眩 1 秒。", s => s.OnHit.Add(St.Stun(1))),
                  U("尖石", "裂痕中留下尖石 3 秒，每秒造成 40% 武器傷害。", s => s.Leave = new ZoneSpec { Coef = 0.2f, Dur = 3, Radius = 2.4f, El = Element.Physical }));
        N("ws_storm", SkillCat.Mastery, 14, "喚雷", "lightning-storm", "在目標位置召來雷雲 5 秒，每秒對範圍內敵人造成 {0}% 武器傷害的閃電傷害。")
            .Zone(0.4f, 16f, 5f, 5f, 0.5f).El(Element.Lightning).Res(30, 8).Cast("Spell_Simple_Shoot", 1.3f, 0.24f, 0.46f, "wind_cast", "storm_bolt").With(s => { s.Form = 3; s.Lucky = 0.12f; s.Flag("per_second"); })
            .Ups_(U("廣雲", "作用範圍擴大 25%。", s => s.Radius *= 1.25f),
                  U("驚雷", "每次雷擊有 20% 機率使敵人暈眩 0.5 秒。", s => s.OnHit.Add(St.Stun(0.5f, 0.2f))),
                  U("隨行", "雷雲會跟隨你移動。", s => s.Flag("follow")));
        N("ws_primal", SkillCat.Ultimate, 20, "荒靈降臨", "bear-face", "化身遠古熊靈 15 秒：傷害提高 35%，傷害減免提高 30%，並以咆哮暈眩周圍敵人 1.5 秒，造成 {0}% 武器傷害。")
            .Buff(15).Res(0, 70).Cast("Idle_Rail_Call", 1.3f, 0.3f, 0.5f, "beast_roar").B_(Stat.DmgAll, 35, 0).B_(Stat.DmgReduction, 30, 0).Hit(St.Stun(1.5f))
            .With(s => { s.Form = 1; s.Coef = 2f; s.CastBurst = 1f; s.CastBurstRadius = 7; s.Flag("giant"); Flagged(s, "g", b => { }); })
            .Ups_(U("復甦", "啟動時恢復 30% 生命上限。", s => s.HealPct = 30),
                  U("久駐", "持續時間延長 5 秒。", s => s.Duration += 5),
                  U("雷霆之怒", "持續期間每次爆擊觸發落雷，對周圍造成 60% 武器傷害。", s => s.Buffs.Find(b => b.Stat == Stat.Count).CritBurst = 0.6f));
        P("ws_p1", 2, "野性", (Stat.DmgCore, 5)); P("ws_p2", 4, "厚毛", (Stat.LifePct, 2), (Stat.Armor, 30));
        P("ws_p3", 10, "風暴之子", (Stat.DmgLightning, 5), (Stat.DmgPhysical, 4)); P("ws_p4", 14, "林息", (Stat.ResourceGen, 6), (Stat.Healing, 4));
        K("kp_ws_feral", "獸性", "變換形態後 4 秒內，造成的傷害提高 20%。");
        K("kp_ws_elder", "古林之語", "人形技能（大地與風暴）的冷卻時間縮短 25%，傷害提高 10%。");
        Sp("ws_bear", "熊靈", "生命上限 +10%，熊靈形技能傷害 +20%。", s => { if (s.Form == 1) s.Coef *= 1.2f; }, (Stat.LifePct, 10));
        Sp("ws_wolf", "狼靈", "攻擊速度 +10%，狼靈形技能傷害 +20%。", s => { if (s.Form == 2) s.Coef *= 1.2f; }, (Stat.AttackSpeed, 10));
        Sp("ws_raven", "鴉靈", "靈息產生 +20%，人形技能傷害 +20%。", s => { if (s.Form == 3) s.Coef *= 1.2f; }, (Stat.ResourceGen, 20));

        // ══════════════════ 鍛身者 Forgebody ══════════════════
        _c = ClassId.Forgebody;
        N("fb_jab", SkillCat.Basic, 1, "連拳", "punch", "迅速出拳，造成 {0}% 武器傷害。產生 9 點氣焰。")
            .Melee(0.36f, 2.6f, 110f).Res(-9).Cast("Punch_Jab", 1.9f, 0.16f, 0.3f, "punch_whoosh", "punch_hit").Alt("Punch_Jab", "Punch_Cross").With(s => s.Lucky = 0.45f)
            .Ups_(U("燼掌", "每第四擊化為燼掌，對周圍所有敵人造成 250% 的火焰傷害。", s => { s.Nth = 4; s.NthMod = n => { n.Coef *= 2.5f; n.Element = Element.Fire; n.Radius = 3.8f; n.Arc = 360; }; }),
                  U("連綿", "施放後攻擊速度提高 3%，持續 4 秒，最多疊加 5 層。", s => s.B_(Stat.AttackSpeed, 3, 4, "連綿", 5)),
                  U("調息", "每次命中恢復 0.8% 生命上限。", s => s.HealOnHit = 0.8f));
        N("fb_palm", SkillCat.Core, 2, "崩山掌", "hand", "推出一道掌勁，對前方直線上的敵人造成 {0}% 武器傷害並將其擊退。")
            .Wave(1.5f, 6.5f, 2.6f).Res(28).Cast("Punch_Cross", 1.2f, 0.3f, 0.55f, "punch_whoosh", "punch_hit").Hit(St.Knock(8)).With(s => s.Lucky = 0.4f)
            .Ups_(U("裂甲", "命中使敵人易傷 3 秒。", s => s.OnHit.Add(St.Vuln(3))),
                  U("炎掌", "轉為火焰傷害，並使敵人燃燒，4 秒內造成 80% 武器傷害。", s => { s.Element = Element.Fire; s.OnHit.Add(St.Burn(0.8f, 4)); }),
                  U("借力", "每命中一名敵人恢復 4 點氣焰。", s => s.GainOnHit = 4));
        N("fb_spin", SkillCat.Core, 5, "燼旋腿", "high-kick", "旋身連踢周圍敵人 3 次，每次造成 {0}% 武器傷害的火焰傷害。")
            .Nova(0.55f, 3.4f, 3).El(Element.Fire).Res(30).Cast("Melee_Hook", 0.8f, 0.16f, 0.6f, "kick_heavy", "punch_hit").With(s => { s.Lucky = 0.25f; s.Flag("spin"); })
            .Ups_(U("掃堂", "命中使敵人暈眩 0.75 秒。", s => s.OnHit.Add(St.Stun(0.75f))),
                  U("烈旋", "作用範圍擴大 30%。", s => s.Radius *= 1.3f),
                  U("蓄勢", "每命中一名敵人恢復 2 點氣焰。", s => s.GainOnHit = 2));
        N("fb_guard", SkillCat.Defensive, 4, "鐵衣", "muscle-up", "凝氣護體 4 秒，獲得 50% 傷害減免。")
            .Buff(4).Res(0, 16).Cast("Sword_Block", 1.8f, 0.08f, 0.25f, "chi_charge").B_(Stat.DmgReduction, 50, 0)
            .Ups_(U("綿長", "持續時間延長 2 秒。", s => s.Duration += 2),
                  U("不動", "持續期間免疫控制效果。", s => Flagged(s, "u", b => b.Unstoppable = true)),
                  U("迸發", "結束時氣勁迸發，對周圍造成 120% 武器傷害。", s => { s.EndBurst = 1.2f; s.EndBurstRadius = 4.5f; }));
        N("fb_dash", SkillCat.Mobility, 7, "踏燼步", "run", "踏火突進，對途經的敵人造成 {0}% 武器傷害的火焰傷害。可儲存 2 次充能。")
            .Dash(0.8f, 9f, 1.6f).El(Element.Fire).Res(0, 7, 2).Cast("Sword_Dash", 1.6f, 0f, 0.3f, "evade", "punch_hit")
            .Ups_(U("蓄銳", "施放後爆擊機率提高 30%，持續 3 秒。", s => s.B_(Stat.CritChance, 30, 3)),
                  U("疾步", "踏燼步可額外儲存 1 次充能。", s => s.Charges = 3),
                  U("火痕", "途經之處留下火焰 3 秒，每秒造成 50% 武器傷害。", s => s.Leave = new ZoneSpec { Coef = 0.25f, Dur = 3, Radius = 1.8f }));
        N("fb_break", SkillCat.Mastery, 10, "破雲擊", "fist", "凝聚全力的一擊，對前方敵人造成 {0}% 武器傷害，使其暈眩 1.5 秒並陷入易傷。")
            .Melee(2.6f, 3.0f, 70f).Res(25, 6).Cast("Melee_Hook", 1.1f, 0.4f, 0.7f, "punch_whoosh", "hit_crit").Hit(St.Stun(1.5f), St.Vuln(3)).With(s => s.Lucky = 0.6f)
            .Ups_(U("乘勝", "對已受控制的敵人傷害提高 40%。", s => { s.DmgVs[StatusId.Stun] = 0.4f; s.DmgVs[StatusId.Freeze] = 0.4f; s.DmgVs[StatusId.Root] = 0.4f; }),
                  U("震山", "攻擊範圍大幅擴大。", s => { s.Arc = 170; s.Radius = 3.8f; }),
                  U("再擊", "以破雲擊擊殺敵人時重置其冷卻時間。", s => s.ResetOnKill = true));
        N("fb_wave", SkillCat.Mastery, 14, "氣焰波", "wind-slap", "推出貫穿敵人的氣焰波，造成 {0}% 武器傷害的火焰傷害。")
            .Proj(1.8f, 16f, 1, -1, 24f).El(Element.Fire).Res(35, 5).Cast("Punch_Cross", 1f, 0.34f, 0.6f, "chi_burst", "fire_impact").With(s => s.Lucky = 0.4f)
            .Ups_(U("餘燼", "命中使敵人燃燒，4 秒內造成 100% 武器傷害。", s => s.OnHit.Add(St.Burn(1f, 4))),
                  U("三疊", "同時推出 3 道氣焰波，各造成 60% 傷害。", s => { s.Count = 3; s.Coef *= 0.6f; s.Spread = 16; }),
                  U("潰散", "氣焰波在終點爆裂，對周圍造成 100% 武器傷害。", s => { s.EndBurst = 1f; s.EndBurstRadius = 3.5f; }));
        N("fb_asura", SkillCat.Ultimate, 20, "日爐化身", "burning-passion", "點燃體內的日爐餘火 12 秒：攻擊速度提高 30%，傷害提高 25%，所有攻擊使敵人燃燒。")
            .Buff(12).El(Element.Fire).Res(0, 60).Cast("Idle_Rail_Call", 1.4f, 0.25f, 0.45f, "chi_charge").B_(Stat.AttackSpeed, 30, 0).B_(Stat.DmgAll, 25, 0)
            .With(s => { Flagged(s, "f", b => b.Burn = 0.4f); s.CastBurstRadius = 7; })
            .Ups_(U("爐爆", "啟動時釋放火焰衝擊波，造成 220% 武器傷害。", s => s.CastBurst = 2.2f),
                  U("淬身", "持續期間造成傷害的 5% 轉化為生命。", s => Flagged(s, "l", b => b.LifeSteal = 5)),
                  U("長燃", "持續時間延長 5 秒。", s => s.Duration += 5));
        P("fb_p1", 2, "拳勁", (Stat.DmgBasic, 6)); P("fb_p2", 4, "銅皮", (Stat.DmgReduction, 1.5f), (Stat.Dodge, 1));
        P("fb_p3", 10, "爐火", (Stat.DmgFire, 5), (Stat.CritDmg, 5)); P("fb_p4", 14, "行氣", (Stat.AttackSpeed, 2), (Stat.ResourceGen, 5));
        K("kp_fb_flow", "連綿不絕", "基本技能每連續命中一次，造成的傷害提高 3%，最多 30%。停止攻擊 3 秒後重置。");
        K("kp_fb_vajra", "金剛", "使敵人暈眩時，獲得相當於生命上限 5% 的屏障。");
        Sp("fb_fire", "火脈", "火焰傷害 +20%，你的所有攻擊使敵人燃燒。", s => { if (s.Coef > 0) s.OnHit.Add(St.Burn(0.2f, 3)); }, (Stat.DmgFire, 20));
        Sp("fb_iron", "鐵脈", "傷害減免 +12%，護甲 +150。", null, (Stat.DmgReduction, 12), (Stat.Armor, 150));
        Sp("fb_wind", "風脈", "攻擊速度 +10%，移動速度 +10%，閃避機率 +5%。", null, (Stat.AttackSpeed, 10), (Stat.MoveSpeed, 10), (Stat.Dodge, 5));

        // ══════════════════ 誓燈騎士 Lampknight ══════════════════
        _c = ClassId.Lampknight;
        N("lk_strike", SkillCat.Basic, 1, "誓擊", "pointy-sword", "以長劍斬擊前方敵人，造成 {0}% 武器傷害。產生 10 點信念。")
            .Melee(0.5f, 3.0f).Res(-10).Cast("Sword_Regular_A", 1.35f, 0.22f, 0.42f, SW, "hit_flesh").Alt(swords).With(s => s.Lucky = 0.5f)
            .Ups_(U("燼光斬", "每第三擊綻放燼光：傷害提高 60%、轉為火焰傷害並擴大範圍。", s => { s.Nth = 3; s.NthMod = n => { n.Coef *= 1.6f; n.Element = Element.Fire; n.Radius = 4.4f; n.Arc = 200; }; }),
                  U("持守", "施放後獲得 2% 傷害減免，持續 4 秒，最多疊加 5 層。", s => s.B_(Stat.DmgReduction, 2, 4, "持守", 5)),
                  U("慈光", "每次命中恢復 0.8% 生命上限。", s => s.HealOnHit = 0.8f));
        N("lk_bash", SkillCat.Core, 2, "盾擊", "shield-bash", "以盾猛擊前方敵人，造成 {0}% 武器傷害並暈眩 1 秒，同時獲得相當於生命上限 6% 的屏障。")
            .Melee(1.3f, 2.8f, 110f).Res(25).Cast("Shield_OneShot", 1.3f, 0.3f, 0.55f, "shield_bash", "hit_metal").Hit(St.Stun(1)).With(s => { s.Barrier = 0.06f; s.BarrierDur = 4; s.Lucky = 0.4f; })
            .Ups_(U("重盾", "暈眩時間延長至 1.5 秒。", s => Rep(s, StatusId.Stun, 1.5f)),
                  U("懲戒", "對暈眩中的敵人傷害提高 30%。", s => s.DmgVs[StatusId.Stun] = 0.3f),
                  U("盾風", "攻擊範圍大幅擴大。", s => { s.Arc = 200; s.Radius = 3.8f; }));
        N("lk_smite", SkillCat.Core, 5, "燼光審判", "sunbeams", "召下一道燼光之柱，短暫延遲後對目標區域造成 {0}% 武器傷害的火焰傷害。")
            .Ground(1.7f, 16f, 3.2f, 0.45f).El(Element.Fire).Res(30).Cast("Spell_Simple_Shoot", 1.4f, 0.22f, 0.44f, "holy_cast", "holy_impact").With(s => s.Lucky = 0.4f)
            .Ups_(U("聖焰", "審判之處留下聖焰 3 秒，每秒造成 40% 武器傷害。", s => s.Leave = new ZoneSpec { Coef = 0.2f, Dur = 3, Radius = 3.2f }),
                  U("昭罪", "命中使敵人易傷 3 秒。", s => s.OnHit.Add(St.Vuln(3))),
                  U("再審", "燼光之柱會再次落下，造成 60% 傷害。", s => { s.ExtraStrikes = 1; s.ExtraMul = 0.6f; s.Flag("same_spot"); }));
        N("lk_aegis", SkillCat.Defensive, 4, "燼光庇護", "shield-reflect", "以燼光護體，獲得相當於生命上限 35% 的屏障，持續 6 秒。")
            .Buff(6).Res(0, 20).Cast("Sword_Block", 1.5f, 0.1f, 0.3f, "barrier_up").With(s => s.Barrier = 0.35f)
            .Ups_(U("堅信", "持續期間額外獲得 15% 傷害減免。", s => s.B_(Stat.DmgReduction, 15, 0)),
                  U("癒光", "施放時立即恢復 15% 生命上限。", s => s.HealPct = 15),
                  U("不屈", "持續期間免疫控制效果。", s => Flagged(s, "u", b => b.Unstoppable = true)));
        N("lk_charge", SkillCat.Mobility, 7, "持盾衝鋒", "shield-impact", "舉盾向前衝鋒，對途經的敵人造成 {0}% 武器傷害，將其擊退並暈眩 0.75 秒。")
            .Dash(1.0f, 10f, 2.0f).Res(0, 9).Cast("Shield_Dash", 1.4f, 0f, 0.4f, "evade", "shield_bash").Hit(St.Knock(6), St.Stun(0.75f))
            .Ups_(U("破陣", "衝鋒終點爆發燼光，對周圍造成 80% 武器傷害。", s => { s.EndBurst = 0.8f; s.EndBurstRadius = 4; }),
                  U("迅馳", "冷卻時間縮短 3 秒。", s => s.Cooldown -= 3),
                  U("激勵", "施放時產生 25 點信念。", s => s.Cost = -25));
        N("lk_aura", SkillCat.Mastery, 10, "守燈光環", "aura", "光環：持續獲得 12% 傷害減免，並每秒恢復 1.5% 生命上限。主動施放：使腳下化為聖地 6 秒，每秒對敵人造成 {0}% 武器傷害的火焰傷害，並持續治療你。")
            .Buff(0).El(Element.Fire).Res(0, 20).Cast("Sword_Block", 1.5f, 0.2f, 0.4f, "consecrate")      // shield on the left arm: the guard, not the two-handed call
            .With(s =>
            {
                s.Coef = 0.35f; s.Flag("per_second", "leave_at_self");
                s.Buffs.Add(new BuffDef { Id = "lk_aura_dr", Name = "守燈光環", Stat = Stat.DmgReduction, Value = 12, Aura = true });
                s.Buffs.Add(new BuffDef { Id = "lk_aura_regen", Name = "守燈光環", Aura = true, RegenPct = 1.5f });
                s.Leave = new ZoneSpec { Coef = 1f, Dur = 6, Radius = 6, HealPct = 1f };
            })
            .Ups_(U("燼輝", "光環同時使你造成的傷害提高 10%。", s => s.Buffs.Add(new BuffDef { Id = "lk_aura_dmg", Name = "守燈光環", Stat = Stat.DmgAll, Value = 10, Aura = true })),
                  U("廣被", "聖地的範圍擴大 30%。", s => s.Leave.Radius *= 1.3f),
                  U("束罪", "聖地中的敵人緩速 30%。", s => s.Leave.OnHit.Add(St.Slow(0.3f, 1))));
        N("lk_lance", SkillCat.Mastery, 14, "燼光之槍", "spear-hook", "擲出貫穿敵人的燼光之槍，造成 {0}% 武器傷害的火焰傷害。")
            .Proj(2.0f, 20f, 1, -1, 36f).El(Element.Fire).Res(35, 5).Cast("OverhandThrow", 1.5f, 0.24f, 0.46f, "holy_cast", "holy_impact").With(s => s.Lucky = 0.4f)
            .Ups_(U("貫日", "每貫穿一名敵人，傷害提高 15%。", s => s.PierceRamp = 0.15f),
                  U("三光", "同時擲出 3 支燼光之槍，各造成 55% 傷害。", s => { s.Count = 3; s.Coef *= 0.55f; s.Spread = 14; }),
                  U("震懾", "命中使敵人暈眩 0.75 秒。", s => s.OnHit.Add(St.Stun(0.75f))));
        N("lk_dawn", SkillCat.Ultimate, 20, "不熄之誓", "sun", "立下不熄之誓 10 秒：生命不會降至 1 以下，造成的傷害提高 40%。")
            .Buff(10).El(Element.Fire).Res(0, 75).Cast("Shield_OneShot", 1.0f, 0.3f, 0.5f, "bell_toll").B_(Stat.DmgAll, 40, 0)
            .With(s => { Flagged(s, "d", b => b.Deathless = true); s.CastBurstRadius = 7; })
            .Ups_(U("破曉", "立誓時綻放燼光，對周圍造成 250% 武器傷害並暈眩 1.5 秒。", s => { s.CastBurst = 2.5f; s.OnHit.Add(St.Stun(1.5f)); }),
                  U("長誓", "持續時間延長 4 秒。", s => s.Duration += 4),
                  U("滿溢", "持續期間施放技能不消耗信念。", s => Flagged(s, "f", b => b.FreeCast = true)));
        P("lk_p1", 2, "盾衛", (Stat.Armor, 50), (Stat.BarrierPct, 4)); P("lk_p2", 4, "虔信", (Stat.LifePct, 2), (Stat.Healing, 4));
        P("lk_p3", 10, "燼光", (Stat.DmgFire, 5), (Stat.DmgCore, 4)); P("lk_p4", 14, "誓言之力", (Stat.ResourceGen, 6), (Stat.Thorns, 40));
        K("kp_lk_retort", "燼光反照", "受到攻擊時有 20% 機率以燼光反擊，對攻擊者周圍造成 150% 武器傷害。");
        K("kp_lk_weight", "誓約之重", "每擁有 10 點信念，造成的傷害提高 2%。");
        Sp("lk_ward", "庇護之誓", "生命上限 +12%，傷害減免 +8%，屏障強度 +20%。", null, (Stat.LifePct, 12), (Stat.DmgReduction, 8), (Stat.BarrierPct, 20));
        Sp("lk_judge", "審判之誓", "火焰傷害 +20%，爆擊機率 +5%。", null, (Stat.DmgFire, 20), (Stat.CritChance, 5));
        Sp("lk_guide", "引燈之誓", "受到治療效果 +25%，冷卻時間縮減 +8%，每秒生命恢復 +10。", null, (Stat.Healing, 25), (Stat.Cdr, 8), (Stat.LifeRegen, 10));

        // ══════════════════ 縛淵術士 Pactbinder ══════════════════  pips = 契印 (max 5)
        _c = ClassId.Pactbinder;
        N("pb_hexbolt", SkillCat.Basic, 1, "咒焰彈", "fire-ray", "射出咒焰，造成 {0}% 武器傷害的暗影傷害。命中獲得 1 枚契印（上限 5）。")
            .Proj(0.42f, 18f).El(Element.Shadow).Cast("Spell_Simple_Shoot", 1.65f, 0.18f, 0.37f, "hex_cast", "hex_impact").With(s => { s.PipGain = 1; s.Lucky = 0.5f; })
            .Ups_(U("血印", "咒焰彈爆擊時額外獲得 1 枚契印。", s => s.PipOnCrit = 1),
                  U("分焰", "命中後彈射至另一名敵人，造成 70% 傷害。", s => s.Bounce = 1),
                  U("蝕力", "命中使敵人造成的傷害降低 10%，持續 3 秒。", s => s.OnHit.Add(St.Weaken(0.1f, 3))));
        N("pb_bind", SkillCat.Core, 2, "締約召喚", "daemon-skull", "召喚你的契約魔為你作戰，其攻擊造成 {0}% 武器傷害。若契約魔已在場，則治癒牠並使其狂暴 6 秒。")
            .Sum("pact_fiend", 1, 0, 0.6f, 8f).Res(30).Cast("Spell_Simple_Enter", 1.5f, 0.3f, 0.5f, "fiend_summon")
            .Ups_(U("血肉之契", "契約魔的生命提高 50%。", s => s.MinionHpMul = 1.5f),
                  U("橫掃", "契約魔的攻擊會波及周圍的敵人。", s => s.Flag("cleave")),
                  U("僕魔", "額外召喚 2 隻小燼魔。", s => s.Flag("imps2")));
        N("pb_lash", SkillCat.Core, 5, "魔契鞭笞", "whip", "消耗 2 枚契印，命令契約魔撲向目標位置重擊，對周圍敵人造成 {0}% 武器傷害的火焰傷害。契約魔不在場時，你自行引爆較弱的咒焰。")
            .Cmd(2.4f, 16f, 4f).El(Element.Fire).Res(10).Cast("Interact", 2f, 0.15f, 0.35f, "pact_command", "fire_impact").With(s => { s.PipReq = 2; s.Lucky = 0.4f; })
            .Ups_(U("震擊", "命中使敵人暈眩 1 秒。", s => s.OnHit.Add(St.Stun(1))),
                  U("狂暴", "作用範圍擴大 30%。", s => s.Radius *= 1.3f),
                  U("焦痕", "重擊之處留下火焰 4 秒，每秒造成 40% 武器傷害。", s => s.Leave = new ZoneSpec { Coef = 0.2f, Dur = 4, Radius = 4 }));
        N("pb_chains", SkillCat.Mastery, 10, "縛淵鎖鏈", "crossed-chains", "射出在敵人之間跳躍的淵鏈，最多命中 4 個目標，每次造成 {0}% 武器傷害的暗影傷害並定身 1.5 秒。獲得 1 枚契印。")
            .Chain(0.8f, 16f, 4).El(Element.Shadow).Res(28).Cast("Spell_Simple_Shoot", 1.3f, 0.24f, 0.46f, "hex_cast", "hex_impact").Hit(St.Root(1.5f)).With(s => { s.PipGain = 1; s.Lucky = 0.25f; })
            .Ups_(U("長鏈", "額外跳躍 2 次。", s => s.Count += 2),
                  U("破防", "命中使敵人易傷 3 秒。", s => s.OnHit.Add(St.Vuln(3))),
                  U("蝕鏈", "被束縛的敵人在 3 秒內受到 80% 武器傷害的暗影傷害。", s => s.OnHit.Add(St.Rot(0.8f, 3))));
        N("pb_ward", SkillCat.Defensive, 4, "血契護符", "magic-shield", "以血為契，獲得相當於生命上限 30% 的屏障，持續 6 秒，並命令契約魔嘲諷周圍的敵人。")
            .Buff(6).Res(0, 18).Cast("Spell_Simple_Enter", 2f, 0.1f, 0.25f, "barrier_up").With(s => { s.Barrier = 0.3f; s.Flag("fiend_taunt"); })
            .Ups_(U("契印湧現", "施放時獲得 2 枚契印。", s => s.PipGain = 2),
                  U("轉嫁", "持續期間獲得 30% 傷害減免。", s => s.B_(Stat.DmgReduction, 30, 0)),
                  U("償還", "結束時恢復 15% 生命上限。", s => s.EndHeal = 15));
        N("pb_step", SkillCat.Mobility, 7, "裂隙步", "portal", "撕開裂隙瞬移至目標位置。原處的裂隙在 1 秒後爆炸，造成 {0}% 武器傷害的暗影傷害。")
            .Blink(0f, 12f, 4f).El(Element.Shadow).Res(0, 10).Cast("Spell_Simple_Shoot", 2f, 0.08f, 0.2f, "rift_open", "hex_impact").With(s => { s.Coef = 1.1f; s.OriginBurst = 1f; s.Flag("no_origin_nova"); })
            .Ups_(U("同行", "契約魔會隨你一同瞬移並恢復 30% 生命。", s => s.Flag("fiend_follow")),
                  U("熟路", "冷卻時間縮短 3 秒。", s => s.Cooldown -= 3),
                  U("淵幕", "抵達時獲得相當於生命上限 15% 的屏障，持續 4 秒。", s => { s.Barrier = 0.15f; s.BarrierDur = 4; }));
        N("pb_rain", SkillCat.Mastery, 14, "淵火之雨", "fire-zone", "在目標區域降下淵火 5 秒，每秒對範圍內敵人造成 {0}% 武器傷害的火焰傷害並使其燃燒。")
            .Zone(0.45f, 17f, 5.5f, 5f, 0.5f).El(Element.Fire).Res(40, 8).Cast("Spell_Simple_Shoot", 1.2f, 0.26f, 0.5f, "fire_cast", "fire_impact").Hit(St.Burn(0.15f, 3)).With(s => { s.Lucky = 0.12f; s.Flag("per_second"); })
            .Ups_(U("燎原", "作用範圍擴大 25%。", s => s.Radius *= 1.25f),
                  U("不息", "持續時間延長 2 秒。", s => s.Duration += 2),
                  U("淵印", "每次灼燒有 15% 機率獲得 1 枚契印。", s => s.PipChance = 0.15f));
        N("pb_gate", SkillCat.Ultimate, 20, "淵門洞開", "magic-portal", "開啟淵門 15 秒：召來一頭熔岩巨魔為你作戰，其重擊造成 {0}% 武器傷害；期間命令契約魔不需消耗契印。")
            .Sum("pact_gate_brute", 1, 15, 2.4f, 8f).El(Element.Fire).Res(0, 80).Cast("Spell_Simple_Enter", 1.1f, 0.45f, 0.75f, "rift_open")
            .With(s => { Flagged(s, "p", b => { b.FreePips = true; b.Dur = 15; }); s.CastBurstRadius = 7; })
            .Ups_(U("淵爆", "淵門開啟時噴發，對周圍造成 250% 武器傷害。", s => s.CastBurst = 2.5f),
                  U("久啟", "持續時間延長 5 秒。", s => { s.SummonDur += 5; s.Buffs.Find(b => b.FreePips).Dur += 5; }),
                  U("殉爆", "巨魔消逝時爆炸，對周圍造成 400% 武器傷害。", s => { s.EndBurst = 4f; s.EndBurstRadius = 6; }));
        P("pb_p1", 2, "咒術研習", (Stat.DmgCore, 5)); P("pb_p2", 4, "魔契護身", (Stat.LifePct, 2), (Stat.ResFire, 5));
        P("pb_p3", 10, "淵火", (Stat.DmgFire, 4), (Stat.DmgShadow, 4)); P("pb_p4", 14, "馭魔", (Stat.MinionDmg, 6), (Stat.MaxResource, 4));
        K("kp_pb_blood", "血契", "你的魔物造成傷害時，你恢復相當於該傷害 10% 的生命。");
        K("kp_pb_full", "契印滿溢", "契印全滿時，你造成的傷害提高 25%。");
        Sp("pb_brute", "巨魔契", "契約魔為熔岩巨魔：皮厚力沉，善於承受傷害。生命上限 +8%。", null, (Stat.LifePct, 8));
        Sp("pb_imps", "群魔契", "契約魔為三隻小燼魔：從遠處投擲火焰。僕從傷害 +15%。", null, (Stat.MinionDmg, 15));
        Sp("pb_wraith", "縛魂契", "契約魔為縛魂：以寒霜彈冰緩敵人。暗影傷害 +15%，冷卻時間縮減 +5%。", null, (Stat.DmgShadow, 15), (Stat.Cdr, 5));
    }
}
