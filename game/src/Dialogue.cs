using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>
/// One spoken line. <see cref="Voice"/> is the stable id of its recording: res://assets/audio/vo/&lt;Voice&gt;.ogg and the
/// matching entry of data/vo_lines.json. <see cref="Speaker"/> is the name shown above the text ("" for the narrator).
/// </summary>
public sealed class DialogueLine
{
    public string Speaker, Text, Voice;
}

/// <summary>A conversation or narration: lines shown one after another by the dialogue panel, then <see cref="OnEnd"/>.</summary>
public sealed class DialogueScript
{
    public string Id;
    public List<DialogueLine> Lines = new();
    public Action OnEnd;
}

/// <summary>
/// Every written and voiced line of the game: narrated intro and ending, the elder's briefings, vendor greetings,
/// villager murmurs, side-quest thanks, boss barks and the heroes' one-liners.
/// Each line is declared once as L(voice profile, voice id, text); data/vo_lines.json mirrors these declarations
/// one to one (id / speaker / voice / text), so keep that shape when editing and regenerate the file afterwards.
/// </summary>
public static class DialogueDb
{
    /// <summary>Voice profile → name shown as the speaker.</summary>
    private static readonly Dictionary<string, string> Speakers = new()
    {
        { "narrator", "旁白" },
        { "elder", "艾爾溫" },
        { "smith", "布蘭" },
        { "merchant", "莉雅" },
        { "occultist", "薇菈" },
        { "jeweler", "歐丹" },
        { "villager_m", "鎮民" },
        { "villager_f", "鎮民" },
        { "boss_gravedigger", "掘墓者・葛魯姆" },
        { "boss_rimelord", "霜骸領主・赫爾蒙" },
        { "boss_bishop", "腐骨主教・瑪爾薩" },
        { "boss_mire_mother", "沼母・艾絲珂" },
        { "boss_sand_tyrant", "鏽砂暴君・伊薩克" },
        { "boss_grand_hush", "寂滅大導師・賽倫" },
        { "boss_osaros", "寂主・奧薩洛斯" },
        { "hero_emberblade", "燼刃衛" },
        { "hero_astromancer", "星術師" },
        { "hero_hunter", "影獵手" },
        { "hero_soulbinder", "喚魂師" },
        { "hero_wildsoul", "荒靈行者" },
        { "hero_forgebody", "鍛身者" },
        { "hero_lampknight", "誓燈騎士" },
        { "hero_pactbinder", "縛淵術士" },
    };

    /// <summary>Loose spellings other modules may use for a hero one-liner (underscores removed) → the canonical suffix.</summary>
    private static readonly Dictionary<string, string> HeroWhat = new()
    {
        ["levelup"] = "levelup", ["level"] = "levelup", ["lvlup"] = "levelup", ["lvl"] = "levelup",
        ["lowlife"] = "lowlife", ["lowhp"] = "lowlife", ["lowhealth"] = "lowlife", ["hurt"] = "lowlife", ["neardeath"] = "lowlife", ["danger"] = "lowlife",
        ["noresource"] = "noresource", ["nores"] = "noresource", ["resource"] = "noresource", ["oom"] = "noresource", ["nomana"] = "noresource",
        ["notenough"] = "noresource", ["notenoughresource"] = "noresource", ["lowresource"] = "noresource",
        ["bagfull"] = "bagfull", ["full"] = "bagfull", ["inventoryfull"] = "bagfull", ["invfull"] = "bagfull", ["bag"] = "bagfull", ["nospace"] = "bagfull",
        ["legendary"] = "legendary", ["firstlegendary"] = "legendary", ["legend"] = "legendary", ["loot"] = "legendary", ["unique"] = "legendary",
    };

    /// <summary>Loose spellings for the three boss barks → the canonical suffix.</summary>
    private static readonly Dictionary<string, string> BossWhat = new()
    {
        ["intro"] = "intro", ["enter"] = "intro", ["entrance"] = "intro", ["start"] = "intro", ["aggro"] = "intro", ["spawn"] = "intro",
        ["phase"] = "phase", ["phase2"] = "phase", ["phase3"] = "phase", ["enrage"] = "phase", ["half"] = "phase",
        ["death"] = "death", ["die"] = "death", ["dead"] = "death", ["defeat"] = "death", ["end"] = "death", ["kill"] = "death",
    };

    private static readonly Dictionary<string, DialogueScript> Scripts = new();
    private static readonly Dictionary<string, DialogueLine> Lines = new();        // voice id → line
    private static readonly Dictionary<string, string> ProfileOf = new();          // voice id → voice profile
    private static readonly List<string> Order = new();                            // voice ids in declaration order
    private static readonly Dictionary<string, List<string>> Pools = new();        // pool id → voice ids that rotate
    private static readonly Dictionary<string, int> PoolAt = new();
    private static readonly Dictionary<string, string> Alias = new();              // loose key → canonical id / pool (null = unknown)
    private static readonly Dictionary<string, int> RequestCount = new();          // script id → times fetched through Get
    private static readonly HashSet<string> Warned = new();

    private static DialogueLine L(string profile, string id, string text)
    {
        var line = new DialogueLine
        {
            Speaker = profile == "narrator" ? "" : Speakers.TryGetValue(profile, out var name) ? name : profile,
            Text = text,
            Voice = id,
        };
        if (!Lines.TryAdd(id, line)) Log.Error("dialogue", $"duplicate voice id '{id}'");
        else { ProfileOf[id] = profile; Order.Add(id); }
        return line;
    }

    private static void S(string id, params DialogueLine[] lines)
    {
        var s = new DialogueScript { Id = id };
        s.Lines.AddRange(lines);
        Scripts[id] = s;
    }

    /// <summary>Registers a rotating pool made of every line whose id starts with one of the prefixes.</summary>
    private static void Pool(string pool, params string[] prefixes)
    {
        var ids = new List<string>();
        foreach (var id in Order)
            foreach (var p in prefixes)
                if (id.StartsWith(p, StringComparison.Ordinal)) { ids.Add(id); break; }
        if (ids.Count > 0) Pools[pool] = ids;
    }

    static DialogueDb()
    {
        // ───────────────────────── narration ─────────────────────────

        S("intro",
            L("narrator", "intro_01", "一千年前，日爐熄了。瑟蘭從此只剩灰暮。"),
            L("narrator", "intro_02", "活下來的人圍著燼火過日子。火在，人就在。"),
            L("narrator", "intro_03", "一座燼塔，守一方人煙。塔與塔之間，是很長的黑。"),
            L("narrator", "intro_04", "如今，燼塔一座接一座地暗了。有人在夜裡吹熄它們。"),
            L("narrator", "intro_05", "他們自稱寂滅教團。他們說，火是痛，寂靜才是慈悲。"),
            L("narrator", "intro_06", "而你是持燼者。血肉裡，帶著一點不肯熄的火。"),
            L("narrator", "intro_07", "你循著最後一道火光，走到了殘燈鎮。"),
            L("narrator", "intro_08", "鎮心的大火盆還亮著。只是，已經不太暖了。"));

        S("ending",
            L("narrator", "ending_01", "寂主倒下的時候，沒有聲音。一千年的寂靜，就這樣碎了。"),
            L("narrator", "ending_02", "爐心是冷的。冷得像從來沒有燒過。"),
            L("narrator", "ending_03", "你把手按上去。胸口的燼，自己認得路。"),
            L("narrator", "ending_04", "火從你身上過去，一點也不痛。只是忽然很空。"),
            L("narrator", "ending_05", "然後，日爐亮了。先是一粒，再是一片。"),
            L("narrator", "ending_06", "它燒的是你的燼。從今以後，你在，它就在。"),
            L("narrator", "ending_07", "光越亮，影越深。寂主墜落之處，裂開了無數深淵裂隙。"),
            L("narrator", "ending_08", "灰暮還沒有散。但今天，瑟蘭有了第一個早晨。"));

        // ───────────────────────── the elder: main story ─────────────────────────

        S("elder_first",
            L("elder", "elder_first_01", "站近些，孩子。這火不比從前，站遠了就照不到你。"),
            L("elder", "elder_first_02", "我是艾爾溫，替這鎮子守燈。守了四十個冬天。"),
            L("elder", "elder_first_03", "你身上有燼。別藏，火盆認得你。它剛才亮了一下。"),
            L("elder", "elder_first_04", "我直說吧。火盆快撐不住了。荒原的燼塔一滅，它就孤了。"),
            L("elder", "elder_first_05", "東邊的斷燈哨站，塔還立著，火卻被人掐了。"),
            L("elder", "elder_first_06", "掐火的人叫葛魯姆。他從前是我們的守墓人。"),
            L("elder", "elder_first_07", "如今他把親手埋下的人一個個挖出來，交給寂滅教團。"),
            L("elder", "elder_first_08", "去把塔點亮，再讓他停手。死人該睡，這是鎮上的規矩。"),
            L("elder", "elder_first_09", "回來的時候，火邊有你的位子。我替你留著。"));

        S("elder_act1_return",
            L("elder", "elder_act1_return_01", "塔亮了。我在這裡都看得見。四十年沒這麼亮過。"),
            L("elder", "elder_act1_return_02", "葛魯姆……我會在墓園替他留一塊地。他該睡在自己人旁邊。"),
            L("elder", "elder_act1_return_03", "你帶回來的這枚印，是教團的東西。上頭刻著三把鑰匙。"),
            L("elder", "elder_act1_return_04", "老書管它們叫燼鑰。三把合在一起，能開一道爐門。"),
            L("elder", "elder_act1_return_05", "教團毀不掉它們，只好分給三個人，分得遠遠的。"),
            L("elder", "elder_act1_return_06", "霜脊隘口，赫爾蒙。一個凍死在哨位上的守將。"),
            L("elder", "elder_act1_return_07", "沉眠墓原，瑪爾薩。對著骨頭講道的主教。"),
            L("elder", "elder_act1_return_08", "瘴泣沼澤，艾絲珂。沼裡的東西，都喊她母親。"),
            L("elder", "elder_act1_return_09", "先去哪裡，你自己拿主意。三把都要。沿路的塔，替我點亮。"));

        S("elder_frost_return",
            L("elder", "elder_frost_return_01", "霜之燼鑰。隔著手套都冰手。"),
            L("elder", "elder_frost_return_02", "赫爾蒙守那道關，守到忘了自己早就死了。"),
            L("elder", "elder_frost_return_03", "你讓他下了哨。這是恩情，不是殺戮。"),
            L("elder", "elder_frost_return_04", "去火邊坐一會兒。把指頭烤暖了再走。"));

        S("elder_crypt_return",
            L("elder", "elder_crypt_return_01", "骨之燼鑰。瑪爾薩把它供在祭壇上，當聖物拜。"),
            L("elder", "elder_crypt_return_02", "她說骨頭不會痛。可墓原底下躺的，都是有名有姓的人。"),
            L("elder", "elder_crypt_return_03", "現在他們安靜了。是睡著的那種安靜，不是她要的那種。"),
            L("elder", "elder_crypt_return_04", "你身上有墓土味。莉雅那兒有熱湯，去喝一碗。"));

        S("elder_mire_return",
            L("elder", "elder_mire_return_01", "沼之燼鑰。上頭的泥，怕是洗不掉了。"),
            L("elder", "elder_mire_return_02", "艾絲珂年輕時來鎮上賣過草藥。她的孩子淹死在沼裡。"),
            L("elder", "elder_mire_return_03", "後來，她不肯再讓任何東西死透。教團最會找這種傷心人。"),
            L("elder", "elder_mire_return_04", "靴子脫了，擱在火邊烤。沼裡的濕氣會跟人一輩子。"));

        S("elder_act5_brief",
            L("elder", "elder_act5_brief_01", "三把都在了。擺在一起，它們自己會發燙。你摸摸看。"),
            L("elder", "elder_act5_brief_02", "鎖在南邊。鏽砂廢都，黃銅王朝的舊都。"),
            L("elder", "elder_act5_brief_03", "末代的王伊薩克，想照著日爐，用黃銅再鑄一個太陽。"),
            L("elder", "elder_act5_brief_04", "他沒鑄成。但他手裡有日爐的殘圖，世上最後一份。"),
            L("elder", "elder_act5_brief_05", "教團殺不了他，就把爐門鎖上，連人帶圖關在裡頭。"),
            L("elder", "elder_act5_brief_06", "開門，把圖帶回來。薇菈看得懂那些字。我老了，看不清了。"));

        S("elder_act5_return",
            L("occultist", "elder_act5_return_01", "圖是真的。我核對了三遍。"),
            L("occultist", "elder_act5_return_02", "日爐點得著。但不能從上面點，得下到爐心，從底下點。"),
            L("occultist", "elder_act5_return_03", "還有一行小字，寫著爐芯的材料。……是持燼者。"),
            L("elder", "elder_act5_return_04", "……我本想晚些再說。"),
            L("elder", "elder_act5_return_05", "老書裡提過。日爐從來不燒柴。它燒的，是帶燼的人。"),
            L("elder", "elder_act5_return_06", "去不去，由你。這句話，我對每一個持燼者都說過。"),
            L("elder", "elder_act5_return_07", "可眼下有更急的事。寂滅聖城的鐘樓，昨夜亮了燈。"),
            L("elder", "elder_act5_return_08", "大導師賽倫要敲鐘了。那口鐘一響，寂主就會醒。攔住他。"));

        S("elder_act6_return",
            L("elder", "elder_act6_return_01", "鐘還是響了。只有一聲。鎮上的火，全矮了半截。"),
            L("elder", "elder_act6_return_02", "別怪自己。賽倫是拿命去撞的鐘。誰也攔不住一個求死的人。"),
            L("elder", "elder_act6_return_03", "那一聲之後，日爐底下裂開了。老書叫那地方燼淵。"),
            L("elder", "elder_act6_return_04", "寂主奧薩洛斯就睡在那裡。現在，他翻身了。"),
            L("elder", "elder_act6_return_05", "我這本冊子上，記著每一個下去的持燼者。沒有一個劃掉。"),
            L("elder", "elder_act6_return_06", "底下有一座餘火前哨。若還有人活著，會在那裡。"),
            L("elder", "elder_act6_return_07", "我不說「回來」了。我只說，火邊的位子，還是你的。"));

        S("elder_epilogue",
            L("elder", "elder_epilogue_01", "你看東邊。那不是火光。那叫天亮。我只在書上讀過。"),
            L("elder", "elder_epilogue_02", "很薄，很淡，像剛生起來的火。可它是真的。"),
            L("elder", "elder_epilogue_03", "冊子上的名字，我劃掉了七個。他們是自己走回來的。"),
            L("elder", "elder_epilogue_04", "你的名字，我一直沒寫上去。我不敢寫。"),
            L("elder", "elder_epilogue_05", "薇菈說，爐芯是你的燼。你在，爐就在。"),
            L("elder", "elder_epilogue_06", "所以別熄。這是老頭子唯一的請求。"),
            L("elder", "elder_epilogue_07", "坐吧。今晚不說寂主，也不說深淵。就烤火。"));

        // ───────────────────────── the elder: reminders while an act is under way, and small talk ─────────────────────────

        L("elder", "elder_remind_act1", "斷燈哨站在東邊。先點塔，再找葛魯姆。路上別應陌生的聲音。");
        L("elder", "elder_remind_keys", "三把燼鑰，三個方向。先去哪裡都行，只是別空著手回來。");
        L("elder", "elder_remind_act5", "鏽砂廢都在南邊。風沙大，把口鼻蒙好。爐門只認鑰匙。");
        L("elder", "elder_remind_act6", "聖城的鐘樓還亮著燈。賽倫在等一個時辰。別讓他等到。");
        L("elder", "elder_remind_act7", "燼淵在日爐底下。一直往下走。看見餘火前哨，就歇一歇。");
        L("elder", "elder_remind_after", "裂隙還在往外滲寂靜。你每去一趟，東邊的光就亮一分。");

        L("elder", "elder_idle_1", "火盆今晚燒得穩。是你帶回來的。");
        L("elder", "elder_idle_2", "我每天數一遍鎮上的燈。今天，一盞沒少。");
        L("elder", "elder_idle_3", "年輕時我也帶過燼。後來涼了。涼了的人，就來守燈。");

        // ───────────────────────── town services ─────────────────────────

        L("smith", "smith_greet_1", "要打還是要修？說快點，爐子不等人。");
        L("smith", "smith_greet_2", "刀口鈍了就拿來。別等它害死你，才想起我。");
        L("smith", "smith_greet_3", "鐵不騙人。你給它多少火候，它還你多少硬。");
        L("smith", "smith_idle_1", "我這爐火，是從大火盆借的種。鎮上的東西，都是一家。");
        L("smith", "smith_idle_2", "你回來了。好。我少刻一塊墓牌。");

        L("merchant", "merchant_greet_1", "看看吧。都是從熄了燈的地方撿回來的，便宜。");
        L("merchant", "merchant_greet_2", "金幣我收，故事我也收。外頭有什麼消息？");
        L("merchant", "merchant_greet_3", "別嫌貴。這年頭，敢出鎮門進貨的只剩我了。");
        L("merchant", "merchant_idle_1", "鍋裡有湯，不要錢。喝完再買東西。");
        L("merchant", "merchant_idle_2", "我走過六座熄了燈的鎮子。這裡是第七座。我不想再走了。");

        L("occultist", "occultist_greet_1", "東西放下。桌上的瓶子，一個都別碰。");
        L("occultist", "occultist_greet_2", "威能不會消失，只會換個地方待著。我替它搬家。");
        L("occultist", "occultist_greet_3", "面具？戴著，對你我都好。問下一個問題。");
        L("occultist", "occultist_idle_1", "你的燼今天很吵。是睡不好，還是殺得太多？");
        L("occultist", "occultist_idle_2", "我讀過教團的經文。寫得很美。這才是可怕的地方。");

        L("jeweler", "jeweler_greet_1", "把石頭放在我手心。眼睛不行了，手還認得。");
        L("jeweler", "jeweler_greet_2", "寶石是凍住的火。得替它找個合適的窩。");
        L("jeweler", "jeweler_greet_3", "慢慢挑。好石頭不怕人看。");
        L("jeweler", "jeweler_idle_1", "這顆是暖的。帶著它，夜裡手不會抖。");
        L("jeweler", "jeweler_idle_2", "從前我替新娘鑲戒指，現在替戰士鑲。都一樣，都是盼人回來。");

        // ───────────────────────── villagers ─────────────────────────

        L("villager_m", "villager_m_1", "火盆又矮了一截……不，是我眼花。是我眼花。");
        L("villager_m", "villager_m_2", "我兄弟去了哨站，三個冬天了。塔亮的那晚，我夢見他。");
        L("villager_m", "villager_m_3", "夜裡別應聲。外頭喊你名字的，不一定是人。");
        L("villager_m", "villager_m_4", "持燼者？借個火行嗎。就暖暖手。");
        L("villager_f", "villager_f_1", "孩子問我太陽長什麼樣。我說，像火盆，只是掛在天上。");
        L("villager_f", "villager_f_2", "莉雅的湯又淡了。淡就淡吧，是熱的就好。");
        L("villager_f", "villager_f_3", "我替每個出鎮的人點一盞燈。你那盞，我添過油了。");
        L("villager_f", "villager_f_4", "噓。聽見沒有？……沒有鐘聲。那就好。");
        L("villager_m", "dawn_villager_m", "天邊那道白，我娘說叫曙光。我一直以為是哄小孩的。");
        L("villager_f", "dawn_villager_f", "今早我家的雞叫了。牠這輩子頭一回知道，該什麼時候叫。");

        // ───────────────────────── side quests: what the giver says when the work is done ─────────────────────────

        S("side_wastes_elites_done",
            L("smith", "side_wastes_elites_done", "六個。行了。我徒弟在哨站欠下的命，算是討回來了。拿著，別推。"));
        S("side_wastes_cellar_done",
            L("merchant", "side_wastes_cellar_done", "地窖清乾淨了？那批貨我不要了，沾了死人氣。工錢照付。"));
        S("side_frost_altars_done",
            L("occultist", "side_frost_altars_done", "埋了一千年還燒著？有意思。這是謝禮，也是封口費。"));
        S("side_frost_post_done",
            L("elder", "side_frost_post_done", "哨站裡的人，你帶回來了。冊子上，我今天能劃掉幾個名字。"));
        S("side_crypt_tomb_done",
            L("jeweler", "side_crypt_tomb_done", "圖騰砸了？好。我老伴睡在那裡。她生前最怕吵。"));
        S("side_crypt_elites_done",
            L("smith", "side_crypt_elites_done", "教團的鐵是好鐵，只是心打歪了。熔了重打，還能用。這份歸你。"));
        S("side_mire_events_done",
            L("merchant", "side_mire_events_done", "沼路又能走了。我的同行欠你一條命，我先替他們墊上。"));
        S("side_mire_lair_done",
            L("occultist", "side_mire_lair_done", "她們的藥方我抄完了。有一半，是真能救人的。可惜了。"));
        S("side_sands_workshop_done",
            L("smith", "side_sands_workshop_done", "我師父的師父，就是從那座工坊逃出來的。爐子終於能歇了。"));
        S("side_sands_altars_done",
            L("jeweler", "side_sands_altars_done", "手伸過來。嗯，是暖的。你摸過那些祭壇了。這顆石頭配你。"));
        S("side_sanctum_cloister_done",
            L("merchant", "side_sanctum_cloister_done", "我妹妹出來了。不說話，只是一直哭。夠了，會哭就是活著。"));
        S("side_sanctum_elites_done",
            L("occultist", "side_sanctum_elites_done", "十張面具。和我這張，出自同一個匠人。別問。拿去。"));
        S("side_abyss_forge_done",
            L("smith", "side_abyss_forge_done", "你見過日爐的舊爐膛了。它還熱嗎？……算了，別告訴我。"));
        S("side_abyss_events_done",
            L("elder", "side_abyss_events_done", "前哨捎話上來了。他們說，有人替他們守了三夜，總算睡了一覺。"));

        // ───────────────────────── bosses: entrance, phase change, death ─────────────────────────

        L("boss_gravedigger", "boss_gravedigger_intro", "我埋了他們四十年。如今，只是請他們起來走走。");
        L("boss_gravedigger", "boss_gravedigger_phase", "起來！都起來！你們不是一直喊土裡冷嗎！");
        L("boss_gravedigger", "boss_gravedigger_death", "替我……把土填上。填實一點……");

        L("boss_rimelord", "boss_rimelord_intro", "口令。沒有口令的，一律不得過關。");
        L("boss_rimelord", "boss_rimelord_phase", "一千個冬天，沒有人來換哨。那我就繼續守。");
        L("boss_rimelord", "boss_rimelord_death", "終於……換哨了。這道關，交給你了。");

        L("boss_bishop", "boss_bishop_intro", "噓。聖所之內，連心跳都算喧嘩。");
        L("boss_bishop", "boss_bishop_phase", "骨頭不喊痛。讓我替你卸下這身多餘的血肉。");
        L("boss_bishop", "boss_bishop_death", "我聽見了……好吵……原來寂靜裡，全是哭聲……");

        L("boss_mire_mother", "boss_mire_mother_intro", "小聲點。我的孩子們，好不容易才睡著。");
        L("boss_mire_mother", "boss_mire_mother_phase", "你吵醒他們了。那就由你來哄。到沼底去哄。");
        L("boss_mire_mother", "boss_mire_mother_death", "別怕，孩子們……媽媽這就下來陪你們……");

        L("boss_sand_tyrant", "boss_sand_tyrant_intro", "又一個來偷火的。我的太陽，只差最後一把火。");
        L("boss_sand_tyrant", "boss_sand_tyrant_phase", "黃銅不朽！王不朽！是你們的血肉太不耐燒！");
        L("boss_sand_tyrant", "boss_sand_tyrant_death", "圖……拿去吧。別像我……只造出一個空殼……");

        L("boss_grand_hush", "boss_grand_hush_intro", "你是來聽鐘的嗎，持燼者？坐。只響一聲，不會太久。");
        L("boss_grand_hush", "boss_grand_hush_phase", "火是痛。你燒了一路，不累嗎？讓我替你吹熄它。");
        L("boss_grand_hush", "boss_grand_hush_death", "你攔得住我……攔不住鐘。——聽。");

        L("boss_osaros", "boss_osaros_intro", "我替日爐添了一輩子的柴。你知道，柴是什麼嗎？");
        L("boss_osaros", "boss_osaros_phase", "是人。是你這樣的人。我熄了它，因為我數不下去了。");
        L("boss_osaros", "boss_osaros_death", "那就燒吧……只是記得……每一道光，都有名字……");

        // ───────────────────────── heroes: level up, low life, out of resource, bag full, first legendary ─────────────────────────

        L("hero_emberblade", "hero_emberblade_levelup", "劍更沉了。很好，沉的東西才砍得深。");
        L("hero_emberblade", "hero_emberblade_lowlife", "甲裂了……還站得住。還站得住。");
        L("hero_emberblade", "hero_emberblade_noresource", "怒焰不夠。再挨幾下，就夠了。");
        L("hero_emberblade", "hero_emberblade_bagfull", "背不動了。得丟點東西。");
        L("hero_emberblade", "hero_emberblade_legendary", "這東西裡頭有火。不是我的火。");

        L("hero_astromancer", "hero_astromancer_levelup", "又看懂了一顆星。可惜天上早就沒有星了。");
        L("hero_astromancer", "hero_astromancer_lowlife", "血肉真是不可靠的容器……撐住。");
        L("hero_astromancer", "hero_astromancer_noresource", "靈能見底了。等一等，讓它漲回來。");
        L("hero_astromancer", "hero_astromancer_bagfull", "裝不下了。學問不佔地方，這些破銅爛鐵佔。");
        L("hero_astromancer", "hero_astromancer_legendary", "有意思。這上面的紋路，是活的。");

        L("hero_hunter", "hero_hunter_levelup", "手更穩了。下一箭，不必瞄那麼久。");
        L("hero_hunter", "hero_hunter_lowlife", "血流得太多了。該退一步。");
        L("hero_hunter", "hero_hunter_noresource", "喘口氣。精力還沒回來。");
        L("hero_hunter", "hero_hunter_bagfull", "滿了。獵人不該背這麼多。");
        L("hero_hunter", "hero_hunter_legendary", "好貨。原主人大概死得不甘心。");

        L("hero_soulbinder", "hero_soulbinder_levelup", "亡者們，又多告訴了我一些事。");
        L("hero_soulbinder", "hero_soulbinder_lowlife", "還不到時候……那邊的，先別來接我。");
        L("hero_soulbinder", "hero_soulbinder_noresource", "魂能枯了。這裡的殘魂太少。");
        L("hero_soulbinder", "hero_soulbinder_bagfull", "行囊滿了。連一根骨頭都塞不進去。");
        L("hero_soulbinder", "hero_soulbinder_legendary", "這上面附著一個名字。我會好好用你的。");

        L("hero_wildsoul", "hero_wildsoul_levelup", "熊靈和狼靈都在叫。牠們知道我變強了。");
        L("hero_wildsoul", "hero_wildsoul_lowlife", "傷得深了……大地，借我一口氣。");
        L("hero_wildsoul", "hero_wildsoul_noresource", "靈息散了。得先打幾下，把它喚回來。");
        L("hero_wildsoul", "hero_wildsoul_bagfull", "背得太多，跑不動了。");
        L("hero_wildsoul", "hero_wildsoul_legendary", "連風都繞著它走。這是老東西。");

        L("hero_forgebody", "hero_forgebody_levelup", "又淬了一回火。這副身子，還能再硬一點。");
        L("hero_forgebody", "hero_forgebody_lowlife", "骨頭在響……很好，還沒斷。");
        L("hero_forgebody", "hero_forgebody_noresource", "氣焰不足。出拳，把它打回來。");
        L("hero_forgebody", "hero_forgebody_bagfull", "拿不下了。拳頭本來就不需要這麼多行李。");
        L("hero_forgebody", "hero_forgebody_legendary", "熱的。像剛出爐。是誰打的？");

        L("hero_lampknight", "hero_lampknight_levelup", "誓言又重了一分。我擔得起。");
        L("hero_lampknight", "hero_lampknight_lowlife", "燈還亮著……我就不會倒。");
        L("hero_lampknight", "hero_lampknight_noresource", "信念不足。……不，是我打得還不夠。");
        L("hero_lampknight", "hero_lampknight_bagfull", "行囊已滿。貪多，不合誓約。");
        L("hero_lampknight", "hero_lampknight_legendary", "願它從前的主人安息。這份光，我接下了。");

        L("hero_pactbinder", "hero_pactbinder_levelup", "契約又添了一條。這回，是我佔便宜。");
        L("hero_pactbinder", "hero_pactbinder_lowlife", "別高興，魔物們。我還沒死，債還得還。");
        L("hero_pactbinder", "hero_pactbinder_noresource", "咒力空了。稍等，利息馬上就到。");
        L("hero_pactbinder", "hero_pactbinder_bagfull", "裝滿了。連小魔都不肯替我背。");
        L("hero_pactbinder", "hero_pactbinder_legendary", "嘖。這東西身上的契，比我的還老。");

        // ───────────────────────── rotating pools ─────────────────────────

        foreach (var npc in new[] { "smith", "merchant", "occultist", "jeweler" })
        {
            Pool(npc + "_greet", npc + "_greet_");
            Pool(npc + "_idle", npc + "_idle_");
            Pool(npc, npc + "_greet_", npc + "_idle_");
        }
        Pool("elder_idle", "elder_idle_");
        Pool("villager_m", "villager_m_");
        Pool("villager_f", "villager_f_");
        Pool("villager", "villager_m_", "villager_f_");
        Pool("villager_dawn", "dawn_villager_");
    }

    // ───────────────────────── scripts ─────────────────────────

    /// <summary>A fresh copy of a script (callers may set OnEnd or edit the list). Null when the id is unknown.</summary>
    public static DialogueScript Get(string id)
    {
        if (id != null) RequestCount[id] = RequestCount.GetValueOrDefault(id) + 1;
        return Fetch(id);
    }

    /// <summary>Like <see cref="Get"/> but without counting as a request; used by the story code itself.</summary>
    internal static DialogueScript Fetch(string id)
    {
        if (id == null || !Scripts.TryGetValue(id, out var s))
        {
            if (Warned.Add("script:" + id)) Log.Warn("dialogue", $"unknown script '{id}'");
            return null;
        }
        var copy = new DialogueScript { Id = s.Id };
        copy.Lines.AddRange(s.Lines);
        return copy;
    }

    public static bool Has(string scriptId) => scriptId != null && Scripts.ContainsKey(scriptId);
    /// <summary>How many times another module asked for a script through <see cref="Get"/> since the game started.</summary>
    public static int Requests(string scriptId) => scriptId == null ? 0 : RequestCount.GetValueOrDefault(scriptId);
    public static IEnumerable<string> ScriptIds => Scripts.Keys;
    /// <summary>Every voiced line in declaration order (voice id).</summary>
    public static IReadOnlyList<string> VoiceIds => Order;
    public static DialogueLine Line(string voiceId) => voiceId != null && Lines.TryGetValue(voiceId, out var l) ? l : null;
    /// <summary>The voice profile (narrator, elder, boss_gravedigger, hero_hunter …) a line is recorded with.</summary>
    public static string Profile(string voiceId) => voiceId != null && ProfileOf.TryGetValue(voiceId, out var p) ? p : null;

    // ───────────────────────── barks ─────────────────────────

    /// <summary>
    /// One-liner for HUD subtitles. Keys: a voice id (boss_gravedigger_intro, hero_hunter_lowlife, elder_remind_keys …)
    /// or a rotating pool (smith, smith_greet, smith_idle, merchant…, villager, villager_m, villager_f, elder_idle).
    /// Loose spellings are accepted: boss_&lt;MonsterDef.Id&gt;_intro with the doubled prefix, hero_&lt;classKey&gt;_low_life,
    /// hero_levelup (current hero) and so on. Unknown keys return "" and are logged once.
    /// </summary>
    public static string Bark(string key) => BarkLine(key)?.Text ?? "";

    /// <summary>The whole line behind a bark key (speaker, text, voice id), or null.</summary>
    public static DialogueLine BarkLine(string key)
    {
        string id = Resolve(key);
        return id != null && Lines.TryGetValue(id, out var l) ? l : null;
    }

    /// <summary>The voice id a bark key resolves to right now (pools rotate), or null.</summary>
    public static string VoiceOf(string key) => BarkLine(key)?.Voice;

    private static string _saidVoice;
    private static long _saidAt;

    /// <summary>
    /// Shows a bark as a HUD subtitle and plays its recording. Returns false when the key is unknown or the game is not
    /// up yet. The same line asked for twice within a few seconds is only said once, so two modules may both call this.
    /// </summary>
    public static bool Say(string key, float seconds = 0f)
    {
        var line = BarkLine(key);
        var g = Game.I;
        if (line == null || g == null) return false;
        try
        {
            long now = System.Environment.TickCount64;
            if (line.Voice == _saidVoice && now - _saidAt < 4000) return true;
            _saidVoice = line.Voice; _saidAt = now;
            string speaker = line.Speaker;
            if (line.Voice.StartsWith("hero_", StringComparison.Ordinal))
            {
                string name = g.Hero?.D.Name;
                if (!string.IsNullOrEmpty(name)) speaker = name;
            }
            float t = seconds > 0f ? seconds : Math.Clamp(1.8f + line.Text.Length * 0.17f, 2.5f, 8f);
            g.Hud?.Subtitle(speaker, line.Text, t);
            g.Audio?.PlayVoice(line.Voice);
            return true;
        }
        catch (Exception e)
        {
            Log.Error("dialogue", $"saying '{key}' failed", e);
            return false;
        }
    }

    /// <summary>Next line id of a rotating pool (starts at a random line, then goes round), or null.</summary>
    public static string NextIn(string pool)
    {
        if (pool == null || !Pools.TryGetValue(pool, out var ids) || ids.Count == 0) return null;
        int at = PoolAt.TryGetValue(pool, out var cur) ? (cur + 1) % ids.Count : Rng.R.Next(ids.Count);
        PoolAt[pool] = at;
        return ids[at];
    }

    private static string Resolve(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (Lines.ContainsKey(key)) return key;
        if (Pools.ContainsKey(key)) return NextIn(key);

        // "hero_levelup" without a class → the current hero's class (not cached: the hero changes between saves)
        if (key.StartsWith("hero_", StringComparison.OrdinalIgnoreCase) && !NamesClass(key.Substring(5)))
        {
            var h = Game.I?.Hero;
            return h == null ? null : Resolve("hero_" + Names.ClassKey(h.Class) + "_" + key.Substring(5));
        }

        if (!Alias.TryGetValue(key, out var found))
        {
            found = Normalise(key);
            Alias[key] = found;
            if (found == null) Log.Warn("dialogue", $"unknown bark '{key}'");
            else Log.Debug("dialogue", $"bark '{key}' → '{found}'");
        }
        if (found == null) return null;
        return Pools.ContainsKey(found) ? NextIn(found) : found;
    }

    private static bool NamesClass(string rest)
    {
        foreach (var ck in Names.ClassKeys)
            if (rest.StartsWith(ck + "_", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Maps a loosely spelled key to a voice id or pool id; null when nothing fits.</summary>
    private static string Normalise(string key)
    {
        string k = key.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_').Replace('.', '_').Replace(':', '_').Replace('/', '_');
        while (k.StartsWith("boss_boss_", StringComparison.Ordinal)) k = k.Substring(5);
        if (k.StartsWith("npc_", StringComparison.Ordinal)) k = k.Substring(4);
        if (Lines.ContainsKey(k) || Pools.ContainsKey(k)) return k;

        int cut = k.LastIndexOf('_');
        if (cut > 0 && cut < k.Length - 1)
        {
            string head = k.Substring(0, cut), tail = k.Substring(cut + 1);
            if (BossWhat.TryGetValue(tail, out var bw))
            {
                if (Lines.ContainsKey(head + "_" + bw)) return head + "_" + bw;
                if (Lines.ContainsKey("boss_" + head + "_" + bw)) return "boss_" + head + "_" + bw;
            }
            // "smith_greeting", "merchant_hello", "villager_murmur" …
            if (Pools.ContainsKey(head))
            {
                if (tail is "greeting" or "greetings" or "hello" or "service" or "open") return Pools.ContainsKey(head + "_greet") ? head + "_greet" : head;
                if (tail is "idle" or "chat" or "murmur" or "talk" or "bark" or "line") return Pools.ContainsKey(head + "_idle") ? head + "_idle" : head;
            }
        }

        if (k.StartsWith("hero_", StringComparison.Ordinal))
        {
            string rest = k.Substring(5);
            foreach (var ck in Names.ClassKeys)
            {
                if (!rest.StartsWith(ck + "_", StringComparison.Ordinal)) continue;
                string what = rest.Substring(ck.Length + 1).Replace("_", "");
                if (HeroWhat.TryGetValue(what, out var hw) && Lines.ContainsKey("hero_" + ck + "_" + hw)) return "hero_" + ck + "_" + hw;
                break;
            }
        }
        return null;
    }
}

/// <summary>
/// Speaks the unprompted lines of the story: the hero at the few moments that matter (a level gained, life running
/// out, the bag filling up, the first legendary find) and the story bosses when a fight begins, turns and ends.
/// Driven by <see cref="Quests"/> (attach / detach / world events), the hero's Changed event and World.Damage.
/// </summary>
internal static class StoryVoice
{
    public const string FlagLegendary = "q_first_legendary";
    private const long LowLifeGapMs = 45000, BagGapMs = 60000, AnyGapMs = 4000, AfterBossGapMs = 6000;
    private const float LowLifeAt = 0.3f;

    private static Hero _h;
    private static bool _wasFull, _legend;
    private static int _legendTries;
    private static long _lowAt, _bagAt, _anyAt, _bossSpokeAt;
    // the boss being fought: only compared by reference, never used after it left the level
    private static Monster _boss;
    private static string _bossId;
    private static bool _bossHasLines;
    private static int _bossPhase;

    public static void Attach(Hero h)
    {
        Detach();
        if (h == null) return;
        _h = h;
        _wasFull = h.BagFull;
        _legend = h.D.Has(FlagLegendary);
        if (!_legend && OwnsLegendary(h.D)) { h.D.Set(FlagLegendary); _legend = true; }      // an older character: nothing to announce
        _legendTries = 0;
        long now = System.Environment.TickCount64;
        _lowAt = now - LowLifeGapMs; _bagAt = now - BagGapMs; _anyAt = now - AnyGapMs; _bossSpokeAt = now - AfterBossGapMs;
        h.Changed += OnChanged;
        World.Damage += OnDamage;
    }

    public static void Detach()
    {
        if (_h != null) _h.Changed -= OnChanged;
        World.Damage -= OnDamage;
        _h = null;
        ForgetBoss();
    }

    /// <summary>A new level was entered: whatever boss was being fought is gone.</summary>
    public static void ForgetBoss()
    {
        _boss = null; _bossId = null; _bossHasLines = false; _bossPhase = 1;
    }

    /// <summary>Called for the "levelup" world event; speaks on the first level gained and then every fifth.</summary>
    public static void LevelUp(string levelArg)
    {
        if (_h == null) return;
        int lvl = int.TryParse(levelArg, out var v) ? v : _h.D.Level;
        if (lvl == 2 || lvl % 5 == 0) Speak("levelup");
    }

    /// <summary>Called for the "boss" world event with a story boss: its last words.</summary>
    public static void BossDied(string bossId)
    {
        if (_h == null || string.IsNullOrEmpty(bossId)) return;
        if (_boss != null && _bossId == bossId) ForgetBoss();
        SpeakBoss(bossId + "_death");
    }

    private static bool OwnsLegendary(SaveData d)
    {
        foreach (var it in d.Bag) if (it != null && it.Rarity >= Rarity.Legendary) return true;
        foreach (var it in d.Equip) if (it != null && it.Rarity >= Rarity.Legendary) return true;
        return false;
    }

    private static void OnChanged()
    {
        var h = _h;
        if (h == null) return;
        try
        {
            if (!_legend && OwnsLegendary(h.D))
            {
                if (Speak("legendary") || ++_legendTries >= 30) { _legend = true; h.D.Set(FlagLegendary); }
            }
            bool full = h.BagFull;
            if (full && !_wasFull)
            {
                long now = System.Environment.TickCount64;
                if (now - _bagAt >= BagGapMs && Speak("bagfull")) _bagAt = now;
            }
            _wasFull = full;
        }
        catch (Exception e) { Log.Error("dialogue", "hero voice (inventory) failed", e); }
    }

    /// <summary>World.Damage fires for every hit in the game: everything here returns early and allocates nothing unless a line is spoken.</summary>
    private static void OnDamage(Actor a, Hit hit, float amount)
    {
        if (_h == null) return;
        var p = World.P;
        if (p == null) return;
        try
        {
            if (!ReferenceEquals(a, p))
            {
                // the hero's side hurt a boss
                if (a is Monster m && m.Brain != null && m.Faction == Faction.Monster) BossFight(m);
                return;
            }
            if (hit.Source is Monster src && src.Brain != null && src.Faction == Faction.Monster) BossFight(src);
            if (amount <= 0f || p.Dead || p.MaxLife <= 0f || p.Life <= 0f || p.Life > p.MaxLife * LowLifeAt) return;
            long now = System.Environment.TickCount64;
            if (now - _lowAt < LowLifeGapMs) return;
            if (Speak("lowlife")) _lowAt = now;
        }
        catch (Exception e) { Log.Error("dialogue", "story voice (damage) failed", e); }
    }

    /// <summary>First blood with a story boss → its entrance line; the first time its fight changes phase → its phase line.</summary>
    private static void BossFight(Monster m)
    {
        if (m.Dead || m.Def == null) return;
        if (!ReferenceEquals(m, _boss))
        {
            _boss = m;
            _bossId = m.Def.Id;
            _bossPhase = Math.Max(1, m.Brain.Phase);
            _bossHasLines = DialogueDb.Line(_bossId + "_intro") != null;
            if (_bossHasLines && _bossPhase == 1) SpeakBoss(_bossId + "_intro");
            return;
        }
        if (!_bossHasLines) return;
        int phase = m.Brain.Phase;
        if (phase <= _bossPhase) return;
        bool first = _bossPhase == 1;
        _bossPhase = phase;
        if (first) SpeakBoss(_bossId + "_phase");
    }

    private static void SpeakBoss(string lineId)
    {
        var g = Game.I;
        if (g == null || DialogueDb.Line(lineId) == null) return;
        if (g.Ui != null && g.Ui.IsOpen("dialogue")) return;
        if (DialogueDb.Say(lineId)) _bossSpokeAt = System.Environment.TickCount64;
    }

    private static bool Speak(string what)
    {
        var g = Game.I;
        if (g == null || _h == null || g.State != Game.GameState.Play) return false;
        if (g.Ui != null && g.Ui.IsOpen("dialogue")) return false;      // never talk over a conversation
        long now = System.Environment.TickCount64;
        if (now - _anyAt < AnyGapMs || now - _bossSpokeAt < AfterBossGapMs) return false;     // nor over a boss
        if (!DialogueDb.Say("hero_" + Names.ClassKey(_h.Class) + "_" + what)) return false;
        _anyAt = now;
        return true;
    }
}
