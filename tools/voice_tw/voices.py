"""The 23 designed voices of the game (VoxCPM2 voice design - no reference audio, nobody's voice).

Each voice is a written DESCRIPTION (identity, timbre, delivery; Simplified Chinese because it is a
control text, never spoken) plus an ANCHOR sentence. The description is used exactly once, to speak the
anchor; every line of that character is then generated as a continuation of its own anchor, which is
what keeps the voice the same from line to line (see README.md, "How a voice stays the same").

Bosses are described for CLARITY (user's decision): low, slow, calm, clean articulation - no hoarse,
gravelly, growling or whispered delivery; the menace comes from the words and the effect chain.
Anchor sentences avoid characters whose reading needs a hint, so the anchor text is what was spoken.
"sex" and "f0": a designed voice must MEASURE as its character - median pitch inside the range (men at most
150 Hz, women at least 165 Hz; the first elder, designed at 172 Hz, was heard as an old woman) and the
male/female classifier of gender.py on the right side. "who" is the intended sex and age, for the audit table.
"f0" is the median pitch range (Hz) a designed voice must fall in: a description does not guarantee
a register ("deep" came out at 220 Hz in one test), so designs are measured and repeated until one fits.
"hnr_min" is the lowest acceptable harmonics-to-noise ratio (dB) of the anchor: below it a voice is rough or
hoarse (the boss the user found too coarse measured 4.2). Bosses must be clean (5.5); gruff humans may be rougher.
"designs" is how many designs may be tried (default 4).
Male voices name their register (男低音 bass / 男中音 baritone): without it "a burly middle-aged man" was designed
at 276, 205 and 158 Hz in three attempts; with 男低音 the final boss came out at 91-100 Hz.
"""

TW = "台湾口音的国语"

VOICES = {
    # ------------------------------------------------------------------ narrator and town
    "narrator": dict(sex="m", who="man, middle-aged storyteller", f0=(80, 135), hnr_min=5.0,
        desc=f"中年男性说书人，{TW}，男低音，声音低沉浑厚，语速缓慢，沉稳内敛，像在火堆旁低声讲述",
        anchor="天色慢慢暗了下來。遠方的山只剩下一道灰色的輪廓，風裡帶著灰燼的味道。"),
    "elder": dict(sex="m", who="old man", f0=(95, 140), hnr_min=3.5, designs=6, explore=True,
        desc=f"年迈的老先生，男低音，{TW}，嗓音低沉沙哑但温和，语速缓慢，吐字清楚",
        anchor="坐下來吧，孩子。火還很暖，我慢慢說給你聽，這座鎮子從前是什麼樣子。"),
    "smith": dict(sex="m", who="man, middle-aged, burly", f0=(85, 140), hnr_min=3.5,
        desc=f"中年壮汉，{TW}，男低音，嗓音低沉厚实，说话直接干脆，语气粗率",
        anchor="鐵要趁熱打，話要趁早說。這把刀我磨了三天，拿去，別弄丟了。"),
    "merchant": dict(sex="f", who="young woman", f0=(200, 330),
        desc=f"年轻女性，{TW}，声音明亮清脆，语气爽朗干练",
        anchor="來來來，看一看，挑一挑。今天的貨都是剛到的，價錢好商量，買多了再算你少一點。"),
    "occultist": dict(sex="f", who="woman, mature", f0=(170, 250),
        desc=f"成熟女性，{TW}，声音清冷平稳，语速从容，带着疏离感",
        anchor="把門關上，不要出聲。這些符文很古老，我需要安靜，才能把每一個字看清楚。"),
    "jeweler": dict(sex="f", who="woman, elderly", f0=(165, 250),
        desc=f"年长的女性，{TW}，声音轻柔温和，说话又轻又慢",
        anchor="慢慢來，不用急。每一顆石頭都有自己的光，要靜下心來，才看得見。"),
    "villager_m": dict(sex="m", who="man, middle-aged", f0=(95, 150),
        desc=f"普通的中年男子，{TW}，男中音，声音朴实，语气有些不安",
        anchor="今年的冬天特別冷。柴火不太夠用，大家晚上都擠在大火盆旁邊取暖。"),
    "villager_f": dict(sex="f", who="woman, middle-aged", f0=(175, 270),
        desc=f"普通的中年妇女，{TW}，声音温和朴实，略带疲惫",
        anchor="孩子們都睡了。我把門窗關好，再添一點柴，希望今晚能平平安安地過去。"),

    # ------------------------------------------------------------------ heroes
    "hero_emberblade": dict(sex="m", who="young man", f0=(85, 140),
        desc=f"青年男性战士，{TW}，男低音，声音低沉坚定，语气冷静克制",
        anchor="我的劍只為守護而揮。站到我身後去，這邊交給我，一步也不會退。"),
    "hero_astromancer": dict(sex="f", who="young woman", f0=(190, 310),
        desc=f"年轻女性学者，{TW}，声音清澈明亮，咬字清楚，语气冷静理性",
        anchor="星辰的軌道從來不會說謊。只要算得夠仔細，答案就寫在夜空之中。"),
    "hero_hunter": dict(sex="f", who="young woman", f0=(165, 240),
        desc=f"年轻女性，{TW}，声音偏低而干净，语气平淡冷静，说话简短，音量不大",
        anchor="別出聲，跟著我的腳步走。獵物就在前面，風向對我們有利。"),
    "hero_soulbinder": dict(sex="m", who="young man", f0=(90, 150),
        desc=f"青年男性，{TW}，男中音，声音空灵低缓，气息平稳，语气幽远淡漠",
        anchor="亡者並沒有走遠，他們只是換了一種方式，安靜地留在我們身邊。"),
    "hero_wildsoul": dict(sex="m", who="man in his prime", f0=(90, 150), hnr_min=3.5,
        desc=f"壮年男性，{TW}，男中音，嗓音粗犷豪爽，中气十足，语速稍快",
        anchor="山林就是我的家！風往哪邊吹，我就往哪邊跑，沒有人追得上我！"),
    "hero_forgebody": dict(sex="m", who="young man", f0=(95, 150),
        desc=f"青年男性武者，{TW}，男中音，声音坚实有力，语气干脆利落",
        anchor="出拳要快，站樁要穩。每天練一千遍，身體自然會記住該怎麼做。"),
    "hero_lampknight": dict(sex="f", who="woman, adult", f0=(170, 250),
        desc=f"成年女性骑士，{TW}，声音沉稳坚定，庄重而温和",
        anchor="只要這盞燈還亮著，我就會守在這邊，不讓任何人受到傷害。"),
    "hero_pactbinder": dict(sex="m", who="man, adult", f0=(95, 150),
        desc=f"成年男性，{TW}，男中音，声音圆润流畅，语气从容，带一点戏谑",
        anchor="別緊張，我只是來談一筆小小的交易。條件很公道，你一定會喜歡的。"),

    # ------------------------------------------------------------------ bosses: clear first
    "boss_gravedigger": dict(sex="m", who="old man", f0=(70, 120), hnr_min=5.5, designs=5,
        desc=f"老年男性，{TW}，男低音，声音非常低沉厚重，语速很慢，吐字清晰，语气疲惫阴沉",
        anchor="土是冷的，也是安靜的。我在這片墓園挖了四十年，每一鏟，我都記得。"),
    "boss_rimelord": dict(sex="m", who="man, middle-aged soldier", f0=(75, 125), hnr_min=5.5, designs=5,
        desc=f"中年男性军人，{TW}，男低音，声音低沉冰冷，语速缓慢平直，吐字清晰，不带感情",
        anchor="風雪不會停，我也不會走。這道關口由我看守，沒有命令，任何人不准通過。"),
    "boss_bishop": dict(sex="f", who="woman, elderly cleric (she in the script)", f0=(175, 290), hnr_min=5.5, designs=5,         # 瑪爾薩 is "她" in the script
        desc=f"年长的女性神职人员，{TW}，声音细而清亮，略带鼻音，语速缓慢庄严，吐字清晰",
        anchor="安靜，孩子。聖堂裡不需要聲音，只需要低下頭，慢慢地閉上眼睛。"),
    "boss_mire_mother": dict(sex="f", who="woman, middle-aged", f0=(165, 235), hnr_min=5.5, designs=5,
        desc=f"中年女性，{TW}，声音低柔，像在哄孩子入睡，语速缓慢，吐字清晰",
        anchor="乖，不要哭。媽媽在這邊，媽媽哪裡也不去，慢慢睡吧，天亮還早呢。"),
    "boss_sand_tyrant": dict(sex="m", who="man, middle-aged king", f0=(90, 150), hnr_min=5.5, designs=5,
        desc=f"中年男性君王，{TW}，男低音，声音低沉浑厚，威严而平静，语气高傲，语速缓慢，吐字清晰",          # round 2 (2026-11-20): the loud, exclaimed first design came out at 232-340 Hz
        anchor="抬起頭來，看著我。這座城是我用黃銅一塊一塊蓋起來的，它不會倒，我也不會。"),
    "boss_grand_hush": dict(sex="m", who="old man", f0=(90, 150), hnr_min=5.5, designs=5,
        desc=f"年长男性，{TW}，男中音，声音轻柔低缓，平静慈祥，语速很慢，吐字清晰",
        anchor="不用害怕，一切都會安靜下來。閉上眼睛，聽，鐘聲很快就要響了。"),
    "boss_osaros": dict(sex="m", who="man, ageless", f0=(65, 125), hnr_min=5.5, designs=5,         # default = the variant picked on Y:\voice_samples\osaros (see OSAROS_DEFAULT)
        desc=None,
        anchor="我在這底下睡了很久，很久。火光來了又走，只有黑暗，一直陪著我。"),

    # ------------------------------------------------------------------ named encounters (BESTIARY.md section 4; added 2026-11-20)
    # stronghold bosses (4.2), world boss (4.5), pinnacle rung 7 (4.6). Clear first, like the other bosses.
    "boss_sh_wastes": dict(sex="m", who="man, middle-aged (swollen outpost captain who swallowed the beacon's ember)", f0=(75, 135), hnr_min=5.5, designs=5,
        desc=f"中年男性，{TW}，男低音，声音低沉厚实，语速缓慢，吐字清晰，固执，压抑着痛苦",          # round 2: "murky, halting" gave a rough voice (HNR 4.0 dB)
        anchor="這座塔的鑰匙，還掛在我的腰上。我是哨長，我說不准走，就沒有人可以走。"),
    "boss_sh_frost": dict(sex="m", who="man, middle-aged castellan (fallen knight)", f0=(80, 135), hnr_min=5.5, designs=5,
        desc=f"中年男性守将，{TW}，男低音，声音低沉坚定，语速平稳，吐字清晰干净，像在城墙上下令，军人的严肃",          # round 2: first design rough (HNR 4.1 dB)
        anchor="弓手上牆，盾手守門。只要我還站在這裡，這座關口就一步也不會讓出去。"),
    "boss_sh_crypt": dict(sex="m", who="man, adult (oath-broken barrow warden)", f0=(85, 140), hnr_min=5.5, designs=5,
        desc=f"成年男性，{TW}，男中音，声音低而沉重，带着愧疚和固执，语速缓慢，吐字清晰",
        anchor="我發過誓，要替死去的人守門。後來門是我自己打開的，所以我得守到最後。"),
    "boss_sh_mire": dict(sex="m", who="old man, huge (stockade master of the bog)", f0=(70, 125), hnr_min=5.0, designs=5,
        desc=f"老年男性，{TW}，男低音，声音非常低沉厚重，慢吞吞的，像从泥里冒出来，但每个字都说得清楚，贪婪而固执",
        anchor="這片泥地上的每一根木頭，都是我一根一根拖回來的。誰也別想拿走一塊。"),
    "boss_sh_sands": dict(sex="m", who="brass custodian (construct; male register)", f0=(85, 140), hnr_min=5.5, designs=5,
        desc=f"男性，{TW}，男中音，声音平板冷淡，像在宣读告示，语速均匀，每个字一样轻重，吐字非常清楚，没有感情",
        anchor="本站營業時間已經結束。請各位旅客帶好隨身物品，依照指示，從東門離開。"),
    "boss_sh_sanctum": dict(sex="m", who="man, adult knight-commander", f0=(85, 140), hnr_min=5.5, designs=5,
        desc=f"成年男性骑士，{TW}，男中音，声音冷静克制，傲慢而有礼，语速缓慢，吐字清晰",
        anchor="這條街已經封起來了。我不想動手，但命令就是命令，請你往回走吧。"),
    "boss_sh_abyss": dict(sex="m", who="man, huge brute (ember-eater)", f0=(70, 125), hnr_min=5.0, designs=5,
        desc=f"男性巨人，{TW}，男低音，声音极低沉，粗大而缓慢，饥饿又贪婪，吐字依然清楚，不嘶哑",
        anchor="好暖，好香。我已經很久沒有吃飽了。這一點火，只夠我塞塞牙縫而已。"),
    "boss_wb_firstbearer": dict(sex="m", who="old man, giant (the oldest fuel-bearer)", f0=(65, 120), hnr_min=5.0, designs=5,
        desc=f"非常年迈的男性巨人，{TW}，男低音，声音极其低沉浑厚，说话很慢很少，像搬了一辈子重物，疲惫但坚定，吐字清楚",
        anchor="這條路，我走了一千年。山上砍下來的柴，一捆一捆，全都是我背下來的。"),
    "boss_firstkeeper": dict(sex="f", who="woman, adult (the first lamp-keeper; warm)", f0=(170, 260), hnr_min=5.5, designs=5,
        desc=f"成年女性，{TW}，声音温暖清亮，沉稳坚定，像一位老练的守灯人，语速从容，吐字清晰",
        anchor="第一盞燈是我點起來的。那時候天還沒有黑，我也還不知道，這把火要傳多久。"),
}

# Three descriptions of the final boss for the user's choice page. All ask for clear articulation.
OSAROS_VARIANTS = {
    "deep_clear": dict(label="Deep and clear",
                       desc=f"男低音，{TW}，音调很低，声音浑厚饱满，语速缓慢，吐字清晰标准，冷静克制，带着威严"),
    "deep_cold": dict(label="Deep and cold",
                      desc=f"男低音，{TW}，音调很低，嗓音冰冷平静，语速很慢，每个字都说得清清楚楚，不带感情"),
    "low_soft": dict(label="Low and soft-spoken",
                     desc=f"男性，{TW}，声音低而柔和，轻声但字字清楚，从容缓慢，带着疲惫的平静"),
}
OSAROS_DEFAULT = "low_soft"          # the user's choice on Y:\\voice_samples\\osaros (2026-11-15), at medium effect strength
VOICES["boss_osaros"]["desc"] = OSAROS_VARIANTS[OSAROS_DEFAULT]["desc"]


def ensure(voice: str) -> str:
    """Key of the designed voice for a vo_lines.json "voice" value.

    Known names: themselves. A boss name that is not in the table (new encounters are being added to the
    script) gets a voice of its own, designed from a neutral boss description: deep, slow, clear. Its design
    seeds are derived from the name, so every such boss is a different voice and the same one on every run.
    Give it a proper entry in VOICES when its character is known. Anything else speaks with the narrator."""
    v = (voice or "").strip().lower().replace("-", "_").replace(" ", "_")
    if v in VOICES:
        return v
    if v.startswith("boss_"):
        import zlib
        VOICES[v] = dict(sex="m", who="boss (not in voices.py: neutral boss voice, designed automatically)", f0=(75, 135), hnr_min=5.5, designs=5,
                         seed_base=1000 + 10 * (zlib.crc32(v.encode("utf-8")) % 400), auto=True,
                         desc=f"男低音，{TW}，声音低沉厚实，语速缓慢，吐字清晰，冷峻而有压迫感",
                         anchor="你走得夠遠了。再往前一步，就再也回不去。把火放下，然後離開這個地方。")
        return v
    return "narrator"


def get(voice: str) -> dict:
    """Voice for a vo_lines.json "voice" value (see ensure)."""
    return VOICES[ensure(voice)]
