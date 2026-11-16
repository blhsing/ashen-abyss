"""VoxCPM2 (OpenBMB, Apache-2.0) batch driver - CPU, model loaded once.

    <venvs\\voxcpm python> synth_voxcpm2.py jobs.json
    jobs = [{"out": wav, "text": zh-TW line, "voice": profile, "mode": "design" | "clone",
             "ref_wav": path, "ref_text": transcript, "seed": 1, "pron": "homophone", "script": "t"}]

design: no reference audio - the voice is created from a text description "(描述)臺詞".
clone:  continuation from a reference clip + its transcript (accent and timbre follow the clip).
Readings: "pron": "pinyin" (default) writes a forced reading as pinyin in braces, 寂 -> {ji2}. The README
does not document this, but the model obeys it (measured on this machine: 寂 falls, i.e. mainland jì,
without a hint - even when the voice description asks for a Taiwanese accent - and rises with {ji2}).
"homophone" substitutes a common character with that sound (寂 -> 及), "none" leaves the text alone.
"""
import json
import os
import random
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")   # never depend on the console code page
import time

ROOT = os.environ.get("VT_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "voice_tw"))
MODEL_DIR = os.path.join(ROOT, "models", "voxcpm2")
HERE = os.path.dirname(os.path.abspath(__file__))

sys.path.insert(0, HERE)
import cpu_cap  # noqa: E402  (thread limits; must precede torch)
import stubs  # noqa: E402

# voxcpm is installed with --no-deps. Its denoiser module imports modelscope at import time; we never
# load the denoiser (load_denoiser=False), so a stand-in is enough.
stubs.install("modelscope.pipelines", "modelscope.utils.constant")
for _m, _a in (("modelscope.pipelines", "pipeline"), ("modelscope.utils.constant", "Tasks")):
    if _m in sys.modules and not hasattr(sys.modules[_m], _a):
        setattr(sys.modules[_m], _a, None)

import numpy as np  # noqa: E402
import soundfile as sf  # noqa: E402
import torch  # noqa: E402

import tw_text  # noqa: E402
import vt  # noqa: E402

torch.set_num_threads(cpu_cap.THREADS)

# Voice descriptions (identity, timbre, delivery). Simplified Chinese: the control text is not spoken.
DESIGN = {
    "narrator": "中年男性说书人，台湾口音的国语，声音低沉浑厚，语速缓慢，沉稳内敛，像在火堆旁低声讲述",
    "elder": "年迈的老爷爷，台湾口音的国语，声音苍老而温暖，略带沙哑，语速缓慢，带着疲惫与慈祥",
    "merchant": "年轻女性，台湾口音的国语，声音明亮清脆，语气爽朗干练",
    "hero_emberblade": "青年男性战士，台湾口音的国语，声音低沉坚定，语气冷静克制",
    "boss_osaros": "男性，台湾口音的国语，声音极其低沉沙哑，语速非常缓慢，充满威严与压迫感",
}

# Taiwan reading -> a common character with exactly that sound (TTS input only).
HOMOPHONE = {"ji2": "及", "xi2": "席", "xie3": "寫", "wei2": "維", "fu2": "服", "shu4": "樹", "bo2": "博", "qi2": "其", "xi4": "細"}


def spell(text: str, pron: str, script: str) -> str:
    out = []
    for ch, py, forced, base in tw_text.readings(tw_text.clean(text)):
        if py and forced and py != base and pron != "none":
            if pron == "pinyin":
                out.append("{" + py + "}")
                continue
            if pron == "homophone" and py in HOMOPHONE:
                out.append(HOMOPHONE[py])
                continue
        out.append(ch)
    s = "".join(out)
    return vt.t2s(s) if script == "s" else s


def main():
    jobs = json.load(open(sys.argv[1], encoding="utf-8"))
    from voxcpm import VoxCPM
    import voxcpm.model.voxcpm2 as _m2
    # The checkpoint is configured for bfloat16, which PyTorch runs very slowly on CPU: use float32 there
    # (the library already does the same for Apple MPS).
    _pick = _m2.pick_runtime_dtype
    _m2.pick_runtime_dtype = lambda device, dtype: "float32" if str(device).startswith("cpu") else _pick(device, dtype)
    t0 = time.time()
    model = VoxCPM.from_pretrained(MODEL_DIR, load_denoiser=False, optimize=False, device="cpu")
    sr = model.tts_model.sample_rate
    print(json.dumps(dict(event="loaded", seconds=round(time.time() - t0, 1), threads=torch.get_num_threads(), sample_rate=sr)), flush=True)
    for j in jobs:
        seed = int(j.get("seed", 1))
        script = j.get("script", "t")
        body = spell(j["text"], j.get("pron", "pinyin"), script)
        kw = dict(cfg_value=float(j.get("cfg", 2.0)), inference_timesteps=int(j.get("steps", 10)))   # voxcpm 2.0.3 has no seed argument; torch is seeded below
        if j.get("mode") == "clone":
            text = (f"({j['style']})" if j.get("style") else "") + body
            ref_text = tw_text.clean(j["ref_text"])
            kw.update(prompt_wav_path=j["ref_wav"], prompt_text=vt.t2s(ref_text) if script == "s" else ref_text, reference_wav_path=j["ref_wav"])
        else:
            text = f"({j.get('design') or DESIGN[j['voice']]})" + body
        torch.manual_seed(seed); np.random.seed(seed); random.seed(seed)
        t1 = time.time()
        with torch.inference_mode():
            wav = model.generate(text=text, **kw)
        x = np.asarray(wav, dtype=np.float32).reshape(-1)
        el = time.time() - t1
        os.makedirs(os.path.dirname(j["out"]), exist_ok=True)
        sf.write(j["out"], x, sr, subtype="FLOAT")
        dur = len(x) / sr
        print(json.dumps(dict(out=j["out"], tts_text=text, seconds=round(dur, 2), compute=round(el, 2),
                              rtf=round(el / max(dur, 1e-6), 3), seed=seed), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
