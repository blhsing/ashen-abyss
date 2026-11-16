"""Text front end for Taiwanese Mandarin: readings per character (Taiwan MOE readings where they
differ from the mainland standard), the game's names, and the annotation syntax of each TTS model.

The stored text is never changed; everything here only shapes the TTS *input*.
Readings are pinyin with tone digits (5 = neutral), e.g. "ji2".
"""
from __future__ import annotations

import re

# ---------------------------------------------------------------------------------------------
# Taiwan readings (教育部《重編國語辭典修訂本》) that differ from the PRC standard, per character.
# Only characters whose Taiwan reading is the same in (almost) every word belong here.
TW_CHAR = {
    "寂": "ji2", "熄": "xi2", "息": "xi2", "惜": "xi2", "昔": "xi2", "夕": "xi4", "期": "qi2",
    "危": "wei2", "微": "wei2", "薇": "wei2", "液": "yi4", "企": "qi4", "縛": "fu2", "曙": "shu4",
    "血": "xie3", "誰": "shei2", "薄": "bo2", "擁": "yong3", "擊": "ji2", "跡": "ji1", "蹟": "ji1",
    "績": "ji1", "質": "zhi2", "究": "jiu4", "髮": "fa3", "暫": "zhan4", "識": "shi4", "誼": "yi2",
    "蝸": "gua1", "括": "gua1", "帆": "fan2", "叔": "shu2", "淑": "shu2", "跌": "die2", "突": "tu2",
    "堤": "ti2", "俄": "e4", "諷": "feng4", "偽": "wei4", "攜": "xi1",
}

# Corrections of the plain pypinyin reading that are not Taiwan-specific (OpenCC keeps 著 as 著, which
# pypinyin then reads zhu4; in this script 著 is the particle zhe unless a word below says otherwise).
BASE_FIX = {"著": "zhe5"}

# Words: the game's names (always annotated so rare characters are spoken clearly) and Taiwan
# word readings (full tones where the mainland uses a neutral tone, context-dependent polyphones).
# "*" after a syllable = force the annotation even for models that only annotate when needed.
WORDS = {
    # --- names: people
    "葛魯姆": "ge2 lu3 mu3", "赫爾蒙": "he4 er3 meng2", "瑪爾薩": "ma3 er3 sa4", "艾絲珂": "ai4 si1 ke1",
    "伊薩克": "yi1 sa4 ke4", "賽倫": "sai4 lun2", "奧薩洛斯": "ao4 sa4 luo4 si1", "艾爾溫": "ai4 er3 wen1",
    "布蘭": "bu4 lan2", "莉雅": "li4 ya3", "薇菈": "wei2 la1", "歐丹": "ou1 dan1",
    # --- names: world and places
    "瑟蘭": "se4 lan2", "燼淵": "jin4 yuan1", "燼火": "jin4 huo3", "燼塔": "jin4 ta3", "燼鑰": "jin4 yao4",
    "持燼者": "chi2 jin4 zhe3", "日爐": "ri4 lu2", "灰暮": "hui1 mu4", "寂滅": "ji2 mie4", "寂主": "ji2 zhu3",
    "寂靜": "ji2 jing4", "教團": "jiao4 tuan2", "殘燈鎮": "can2 deng1 zhen4", "霜脊隘口": "shuang1 ji3 ai4 kou3",
    "沉眠墓原": "chen2 mian2 mu4 yuan2", "瘴泣沼澤": "zhang4 qi4 zhao3 ze2", "鏽砂廢都": "xiu4 sha1 fei4 du1",
    "舊都": "jiu4 du1", "聖城": "sheng4 cheng2", "深淵裂隙": "shen1 yuan1 lie4 xi4", "裂隙": "lie4 xi4",
    "大導師": "da4 dao3 shi1", "餘火前哨": "yu2 huo3 qian2 shao4", "斷燈哨站": "duan4 deng1 shao4 zhan4",
    # --- resources / classes that appear in the lines
    "怒焰": "nu4 yan4", "靈能": "ling2 neng2", "魂能": "hun2 neng2", "靈息": "ling2 xi2", "氣焰": "qi4 yan4",
    "咒力": "zhou4 li4", "熊靈": "xiong2 ling2", "狼靈": "lang2 ling2", "殘魂": "can2 hun2",
    # --- Taiwan word readings (full tone instead of the mainland neutral tone, or a different tone)
    "消息": "xiao1 xi2", "利息": "li4 xi2", "可惜": "ke3 xi2", "故事": "gu4 shi4", "地方": "di4 fang1",
    "知道": "zhi1 dao4", "時候": "shi2 hou4", "那裡": "na4 li3", "這裡": "zhe4 li3", "便宜": "pian2 yi2",
    "乾淨": "gan1 jing4", "徒弟": "tu2 di4", "師父": "shi1 fu4", "兄弟": "xiong1 di4", "骨頭": "gu3 tou5",
    "規矩": "gui1 ju3", "主意": "zhu3 yi4", "曙光": "shu4 guang1", "戒指": "jie4 zhi3", "血肉": "xie3 rou4",
    # --- context-dependent polyphones found in the script
    "還你": "huan2 ni3", "還得還": "hai2 dei3 huan2", "同行": "tong2 hang2", "重打": "chong2 da3",
    "睡著": "shui4 zhao2", "點得著": "dian3 de5 zhao2", "附著": "fu4 zhuo2", "殺不了": "sha1 bu4 liao3",
    "數不下去": "shu3 bu2 xia4 qu4", "數一遍": "shu3 yi2 bian4", "守將": "shou3 jiang4", "當聖物": "dang4 sheng4 wu4",
    "睡了一覺": "shui4 le5 yi2 jiao4", "得丟": "dei3 diu1", "得先": "dei3 xian1", "得下到": "dei3 xia4 dao4",
    "得替": "dei3 ti4", "長什麼樣": "zhang3 shen2 me5 yang4", "很長的": "hen3 chang2 de5", "漲回來": "zhang3 hui2 lai2",
    "別應": "bie2 ying4", "挨幾下": "ai2 ji3 xia4", "王朝": "wang2 chao2", "供在": "gong4 zai4", "蒙好": "meng2 hao3",
    "散了": "san4 le5", "沒有散": "mei2 you3 san4", "塞不進": "sai1 bu2 jin4", "劃掉": "hua4 diao4",
}

_PUNCT_MAP = {"……": "…", "⋯⋯": "…", "⋯": "…", "——": "—", "─": "—", "・": "，", "·": "，", "「": "", "」": "", "『": "", "』": ""}


def clean(text: str) -> str:
    """Punctuation the models handle well; quotes dropped, name dots become a short pause."""
    s = text.strip()
    for a, b in _PUNCT_MAP.items():
        s = s.replace(a, b)
    return re.sub(r"\s+", "", s)


def is_han(c: str) -> bool:
    return "㐀" <= c <= "鿿" or "豈" <= c <= "﫿"


def readings(text: str) -> list[tuple[str, str | None, bool, str | None]]:
    """[(char, pinyin-with-tone-digit or None for punctuation, forced, base)] for a Traditional-Chinese line.

    Base reading: pypinyin (phrase-aware, run on the Simplified form). Then the Taiwan character
    table, then the word table (longest match first). forced=True marks readings that came from a
    table, i.e. the ones a model should be told explicitly; base is the plain pypinyin (mainland-leaning)
    reading, so a driver can tell which forced readings actually change something."""
    from pypinyin import Style, pinyin
    import vt
    simp = vt.t2s(text)
    if len(simp) != len(text):                 # OpenCC is 1:1 for the characters we use; be safe anyway
        simp = text
    base = pinyin(simp, style=Style.TONE3, neutral_tone_with_five=True, errors=lambda x: [None] * len(x))
    out: list[list] = []
    for ch, py in zip(text, base):
        p = py[0] if is_han(ch) and py and py[0] else None
        p = BASE_FIX.get(ch, p) if p else p
        out.append([ch, p, False, p])
    for i, (ch, p, _f, _b) in enumerate(out):
        if ch in TW_CHAR:
            out[i][1], out[i][2] = TW_CHAR[ch], True
    for w in sorted(WORDS, key=len, reverse=True):
        start = 0
        syl = WORDS[w].split()
        while True:
            k = text.find(w, start)
            if k < 0:
                break
            if not any(out[k + j][2] == "word" for j in range(len(w))):
                for j in range(len(w)):
                    out[k + j][1], out[k + j][2] = syl[j], "word"
            start = k + len(w)
    return [(c, p, bool(f), b) for c, p, f, b in out]


# ---------------------------------------------------------------------------------------------
# pinyin -> bopomofo (BreezyVoice wants e.g. ㄒㄧㄝ3, neutral = 5)

_INIT = {"b": "ㄅ", "p": "ㄆ", "m": "ㄇ", "f": "ㄈ", "d": "ㄉ", "t": "ㄊ", "n": "ㄋ", "l": "ㄌ", "g": "ㄍ", "k": "ㄎ",
         "h": "ㄏ", "j": "ㄐ", "q": "ㄑ", "x": "ㄒ", "zh": "ㄓ", "ch": "ㄔ", "sh": "ㄕ", "r": "ㄖ", "z": "ㄗ", "c": "ㄘ", "s": "ㄙ"}
_FINAL = {"a": "ㄚ", "o": "ㄛ", "e": "ㄜ", "ai": "ㄞ", "ei": "ㄟ", "ao": "ㄠ", "ou": "ㄡ", "an": "ㄢ", "en": "ㄣ",
          "ang": "ㄤ", "eng": "ㄥ", "er": "ㄦ", "i": "ㄧ", "u": "ㄨ", "v": "ㄩ", "ia": "ㄧㄚ", "ie": "ㄧㄝ", "iao": "ㄧㄠ",
          "iu": "ㄧㄡ", "ian": "ㄧㄢ", "in": "ㄧㄣ", "iang": "ㄧㄤ", "ing": "ㄧㄥ", "ua": "ㄨㄚ", "uo": "ㄨㄛ", "uai": "ㄨㄞ",
          "ui": "ㄨㄟ", "uan": "ㄨㄢ", "un": "ㄨㄣ", "uang": "ㄨㄤ", "ong": "ㄨㄥ", "ve": "ㄩㄝ", "van": "ㄩㄢ", "vn": "ㄩㄣ",
          "iong": "ㄩㄥ", "ueng": "ㄨㄥ", "io": "ㄧㄛ", "ê": "ㄝ"}
_WHOLE = {"zhi": "ㄓ", "chi": "ㄔ", "shi": "ㄕ", "ri": "ㄖ", "zi": "ㄗ", "ci": "ㄘ", "si": "ㄙ",
          "yi": "ㄧ", "ya": "ㄧㄚ", "yo": "ㄧㄛ", "ye": "ㄧㄝ", "yao": "ㄧㄠ", "you": "ㄧㄡ", "yan": "ㄧㄢ", "yin": "ㄧㄣ",
          "yang": "ㄧㄤ", "ying": "ㄧㄥ", "yong": "ㄩㄥ", "yu": "ㄩ", "yue": "ㄩㄝ", "yuan": "ㄩㄢ", "yun": "ㄩㄣ",
          "wu": "ㄨ", "wa": "ㄨㄚ", "wo": "ㄨㄛ", "wai": "ㄨㄞ", "wei": "ㄨㄟ", "wan": "ㄨㄢ", "wen": "ㄨㄣ", "wang": "ㄨㄤ",
          "weng": "ㄨㄥ", "er": "ㄦ", "a": "ㄚ", "o": "ㄛ", "e": "ㄜ", "ai": "ㄞ", "ei": "ㄟ", "ao": "ㄠ", "ou": "ㄡ",
          "an": "ㄢ", "en": "ㄣ", "ang": "ㄤ", "eng": "ㄥ", "n": "ㄣ", "ng": "ㄥ", "m": "ㄇ"}


def split_py(py: str) -> tuple[str, str]:
    """'xie3' -> ('xie', '3')."""
    m = re.fullmatch(r"([a-zü:ê]+)([1-5])?", py.replace("u:", "v").replace("ü", "v"))
    if not m:
        raise ValueError(py)
    return m.group(1), m.group(2) or "5"


def to_bopomofo(py: str) -> str:
    syl, tone = split_py(py)
    if syl in _WHOLE:
        return _WHOLE[syl] + tone
    ini = syl[:2] if syl[:2] in ("zh", "ch", "sh") else syl[:1]
    fin = syl[len(ini):]
    if ini in ("j", "q", "x") and fin.startswith("u"):
        fin = "v" + fin[1:]
    if ini not in _INIT or fin not in _FINAL:
        raise ValueError(py)
    return _INIT[ini] + _FINAL[fin] + tone


_MARK = {"a": "āáǎà", "o": "ōóǒò", "e": "ēéěè", "i": "īíǐì", "u": "ūúǔù", "v": "ǖǘǚǜ"}


def to_marked(py: str) -> tuple[str, str]:
    """'xie3' -> ('x', 'iě'): initial (y/w count as initials) and the final carrying the tone mark,
    the way CosyVoice 3 spells its pinyin tokens."""
    syl, tone = split_py(py)
    ini = syl[:2] if syl[:2] in ("zh", "ch", "sh") else (syl[:1] if syl[:1] in "bpmfdtnlgkhjqxrzcsyw" else "")
    fin = syl[len(ini):]
    if tone != "5":
        t = int(tone) - 1
        # mark goes on a, else o/e, else the last of i/u/v
        for v in ("a", "o", "e"):
            if v in fin:
                fin = fin.replace(v, _MARK[v][t], 1)
                break
        else:
            for k in range(len(fin) - 1, -1, -1):
                if fin[k] in "iuv":
                    fin = fin[:k] + _MARK[fin[k]][t] + fin[k + 1:]
                    break
    return ini, fin.replace("v", "ü")


def sentences(text: str) -> list[str]:
    """Split after sentence-final punctuation, keeping it (。！？… and a trailing —)."""
    parts = re.findall(r"[^。！？!?…]+[。！？!?…—]*|[。！？!?…]+", text)
    out: list[str] = []
    for p in parts:
        if out and not any(is_han(c) for c in p):
            out[-1] += p
        else:
            out.append(p)
    return [p for p in out if p.strip()]


# =============================================================================================
# Production hint tables (VoxCPM2 voice design). A hint replaces ONE character by its reading in the
# TTS input only, e.g. 寂 -> {ji2}. Hints are kept sparse: only where the model, left alone, would say
# something else (it reads mainland by default - measured) or is likely to pick the wrong reading.
#
#   level "tw"    Taiwan MOE reading differs from the mainland one: every character in TW_CHAR, plus
#                 the words in TW_WORD (和 as a conjunction is ㄏㄢˋ in the MOE dictionary)
#   level "tone"  words that keep a full tone in Taiwan where the mainland uses a neutral tone
#   level "poly"  context-dependent polyphones in this script (same on both sides of the strait)
#   level "name"  rare or ambiguous characters in the game's names
# Format: word -> {index of the character inside the word: reading}

TW_WORD = {"和我這張": {0: "han4"}, "熊靈和狼靈": {2: "han4"}}

TONE_WORD = {
    "故事": {1: "shi4"}, "地方": {1: "fang1"}, "知道": {1: "dao4"}, "時候": {1: "hou4"}, "便宜": {1: "yi2"},
    "乾淨": {1: "jing4"}, "徒弟": {1: "di4"}, "師父": {1: "fu4"}, "兄弟": {1: "di4"}, "主意": {1: "yi4"},
    "戒指": {1: "zhi3"}, "母親": {1: "qin1"}, "太陽": {1: "yang2"}, "早晨": {1: "chen2"}, "學問": {1: "wen4"},
    "行李": {1: "li3"}, "時辰": {1: "chen2"}, "火候": {1: "hou4"}, "那裡": {1: "li3"}, "這裡": {1: "li3"},
}

POLY_WORD = {
    "守將": {1: "jiang4"}, "供在": {0: "gong4"}, "當聖物": {0: "dang4"}, "睡著": {1: "zhao2"}, "殺不了": {2: "liao3"},
    "點得著": {2: "zhao2"}, "得下到": {0: "dei3"}, "得丟": {0: "dei3"}, "得先": {0: "dei3"}, "得替": {0: "dei3"},
    "劃掉": {0: "hua4"}, "別應": {1: "ying4"}, "蒙好": {0: "meng2"}, "數一遍": {0: "shu3"}, "數不下去": {0: "shu3"},
    "還你": {0: "huan2"}, "債還得還": {2: "dei3", 3: "huan2"}, "借的種": {2: "zhong3"}, "待著": {0: "dai1"},
    "長什麼樣": {0: "zhang3"}, "很長的": {1: "chang2"}, "重打": {0: "chong2"}, "又重了": {1: "zhong4"},
    "同行": {1: "hang2"}, "睡了一覺": {3: "jiao4"}, "空殼": {1: "ke2"}, "挨幾下": {0: "ai2"},
    "背不動": {0: "bei1"}, "不該背": {2: "bei1"}, "背得太多": {0: "bei1"}, "替我背": {2: "bei1"},
    "漲回來": {0: "zhang3"}, "塞不進": {0: "sai1"}, "附著": {1: "zhuo2"}, "靈息散了": {2: "san4"}, "沒有散": {2: "san4"},
    "王朝": {1: "chao2"}, "廢都": {1: "du1"}, "舊都": {1: "du1"}, "哄": {0: "hong3"},
}

NAME_WORD = {"薇菈": {1: "la1"}, "葛魯姆": {0: "ge2"}, "艾絲珂": {2: "ke1"}}

LEVELS_ALL = ("tw", "tone", "poly", "name")
_VOX_PUNCT = {"—": "，", "～": "，", "~": "，"}


def hints(text: str, levels=LEVELS_ALL) -> list[tuple[str, str | None]]:
    """[(char, reading or None)] for the cleaned line; reading is set where a hint should be given."""
    s = clean(text)
    out: list[list] = [[c, None] for c in s]
    tables = []
    if "tw" in levels:
        for i, c in enumerate(s):
            if c in TW_CHAR:
                out[i][1] = TW_CHAR[c]
        tables.append(TW_WORD)
    if "tone" in levels:
        tables.append(TONE_WORD)
    if "poly" in levels:
        tables.append(POLY_WORD)
    if "name" in levels:
        tables.append(NAME_WORD)
    for table in tables:
        for word in sorted(table, key=len, reverse=True):
            start = 0
            while (k := s.find(word, start)) >= 0:
                for j, py in table[word].items():
                    out[k + j][1] = py
                start = k + len(word)
    return [(c, p) for c, p in out]


def to_voxcpm(text: str, levels=LEVELS_ALL, script: str = "s") -> str:
    """TTS input for VoxCPM2: hinted characters as {pinyin}, dashes as commas, everything else as
    characters - Simplified by default. Measured on this machine: with Traditional input the model
    stumbles on some characters (湯 came out as ㄉㄧㄠ in five takes out of five, 汤 was right at once);
    the script of the input does not change the voice, and Taiwan readings are hinted either way."""
    hs = hints(text, levels)
    chars = "".join(c for c, _ in hs)
    if script == "s":
        import vt
        conv = vt.t2s(chars).replace("著", "着")           # OpenCC keeps 著; in this script it is always the 着 word
        if len(conv) == len(chars):
            chars = conv
    parts = []
    for (orig, p), c in zip(hs, chars):
        parts.append("{" + p + "}" if p else _VOX_PUNCT.get(c, c))
    s = "".join(parts)
    s = re.sub(r"([。！？…])，", r"\1", s)          # a dash right after a full stop: no extra comma
    return re.sub(r"^，+", "", s)


def hint_list(text: str, levels=LEVELS_ALL) -> list[str]:
    """Human-readable list of the hints given for a line, e.g. ['寂=ji2', '血=xie3']."""
    return [f"{c}={p}" for c, p in hints(text, levels) if p]
