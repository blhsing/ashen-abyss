"""The listening test: the same six samples for every candidate model.

Lines are real lines from Y:\\game\\data\\vo_lines.json (two consecutive lines are read as one take
where that brings more of the game's names into a sample). e and f are one synthesis: e is the dry
model output, f adds the boss effect chain.
"""
import json
import os

import vt

SAMPLES = [
    dict(key="a_narrator", voice="narrator", role="Narrator", ids=["intro_05", "intro_06"], fx=None,
         note="Listen for: 寂 twice (Taiwan ㄐㄧˊ jí, mainland jì), 血 (Taiwan ㄒㄧㄝˇ xiě, mainland xuè), "
              "熄 (Taiwan ㄒㄧˊ xí, mainland xī). Names: 寂滅教團, 持燼者."),
    dict(key="b_elder", voice="elder", role="Elder 艾爾溫 (old man)", ids=["elder_act6_return_03", "elder_act6_return_04"], fx=None,
         note="Listen for: 地方 and 那裡 with full tones (Taiwan ㄉㄧˋ ㄈㄤ, ㄋㄚˋ ㄌㄧˇ; mainland uses a neutral second syllable), "
              "寂 (ㄐㄧˊ). Names: 日爐, 燼淵, 寂主奧薩洛斯."),
    dict(key="c_merchant", voice="merchant", role="Merchant 莉雅 (young woman)", ids=["merchant_greet_2"], fx=None,
         note="Listen for: 故事 (Taiwan ㄍㄨˋ ㄕˋ, mainland gùshi) and 消息 (Taiwan ㄒㄧㄠ ㄒㄧˊ, mainland xiāoxi)."),
    dict(key="d_hero", voice="hero_emberblade", role="Hero 燼刃衛 (low male)", ids=["hero_emberblade_levelup"], fx=None,
         note="Retroflex check: 沉 ×2, 深, 砍得 - how strongly ㄔ/ㄕ are curled."),
    dict(key="e_boss_dry", voice="boss_osaros", role="Boss 寂主・奧薩洛斯, DRY", ids=["boss_osaros_intro"], fx=None,
         note="No effects. Listen for: 知道 (Taiwan ㄓ ㄉㄠˋ, mainland zhīdao), 柴 ×2, 什麼. Name: 日爐."),
    dict(key="f_boss_fx", voice="boss_osaros", role="Boss 寂主・奧薩洛斯, WITH effect chain", ids=["boss_osaros_intro"], fx="boss_osaros", same_as="e_boss_dry",
         note="The same take as e, plus the boss chain: five semitones down over an octave-down double, slowed to 84 %, "
              "saturation, vast reverb."),
]

# Kokoro v1.1-zh speaker ids (same picks as the fallback tool) -> baseline voice and synthetic reference clips.
VOICES = {
    "narrator": dict(kokoro_sid=63, ref_speed=0.90, base_speed=0.92, desc_zh="低沉、緩慢、貼近的男性說書人"),
    "elder": dict(kokoro_sid=93, ref_speed=0.90, base_speed=0.98, desc_zh="年邁、緩慢、溫暖的老先生"),
    "merchant": dict(kokoro_sid=44, ref_speed=1.0, base_speed=1.0, desc_zh="明亮的年輕女子"),
    "hero_emberblade": dict(kokoro_sid=70, ref_speed=1.0, base_speed=1.0, desc_zh="低沉的男性戰士"),
    "boss_osaros": dict(kokoro_sid=68, ref_speed=0.88, base_speed=1.0, desc_zh="極低沉、緩慢的男聲"),
}

# Read by Kokoro to make the synthetic reference clips (no character with a Taiwan/mainland reading difference).
REF_TEXT = "夜深了，爐火還在燒。我坐在窗邊，聽遠處的風慢慢吹過山谷，心裡很安靜。"

# The Taiwanese reference: first sentence of the example prompt bundled with the official BreezyVoice
# repository (data/example.wav, Apache-2.0 repo; a Taiwanese female speaker, 16 kHz). Per role it is
# pitch/formant-shifted (and slowed) with our own DSP so the five roles do not share one voice.
BUNDLED_TEXT = "在密碼學中，加密是將明文資訊改變為難以讀取的密文內容，使之不可讀的方法。"
BUNDLED_CUT = (0.80, 9.10)                    # seconds inside data/example.wav
TW_SHIFT = {                                  # semitones (formants move with the pitch), tempo
    "narrator": (-5.5, 0.94), "elder": (-4.6, 0.90), "merchant": (0.8, 1.0),
    "hero_emberblade": (-3.8, 1.0), "boss_osaros": (-6.5, 0.90),
}

STAGE = os.path.join(vt.ROOT, "stage", "samples")
REFS = os.path.join(vt.ROOT, "stage", "refs")
OUT_DIR = r"Y:\voice_samples"


def line_texts() -> dict[str, str]:
    """key -> text (dry keys only)."""
    lines = {l["id"]: l for l in json.load(open(vt.VO_LINES, encoding="utf-8"))}
    return {s["key"]: "".join(lines[i]["text"] for i in s["ids"]) for s in SAMPLES if not s.get("same_as")}
