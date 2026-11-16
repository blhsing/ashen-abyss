"""Prose for the listening-test page (index.html / README.txt). Plain facts measured on 2026-11-14/15;
the numbers quoted here come from Y:\\voice_samples\\report.json and the logs under %VT_ROOT%\\logs.
Strings may contain simple HTML (<b>, <code>); README.txt strips the tags."""

LISTEN = [
    "<b>Accent first.</b> Does the speaker sound like someone from Taiwan? Kokoro (first column) is mainland Mandarin; every other column "
    "is an attempt to do better. Rows a, b, c and e contain words whose Taiwan reading differs from the mainland one "
    "(寂 ㄐㄧˊ, 血 ㄒㄧㄝˇ, 熄 ㄒㄧˊ, 消息 ㄒㄧㄠ ㄒㄧˊ, and 故事 / 地方 / 那裡 / 知道 with a full tone on the second syllable).",
    "<b>Consonants.</b> Row d (沉, 沉, 砍得深) and row e (柴, 知道, 是什麼) show how strongly ㄓ ㄔ ㄕ are curled. A strong curl sounds mainland.",
    "<b>Judge accent, not timbre, in the four \"bundled Taiwanese prompt\" columns.</b> They all use one reference speaker (a woman), shifted in "
    "pitch by our DSP to stand in for the narrator, elder, hero and boss. The male roles there are a shifted female voice and may sound "
    "odd; production would use proper reference voices (see the recommendation).",
    "<b>Acting.</b> The narrator should be slow and intimate, the elder old and warm, the merchant bright. Does the model perform the line "
    "or just read it?",
    "<b>Sound quality.</b> BreezyVoice produces 22 kHz audio (duller still when prompted with the 16 kHz bundled clip), CosyVoice 3 24 kHz, "
    "VoxCPM2 48 kHz.",
    "<b>Row f</b> uses the same effect chain for every column, so differences there come from the dry take in row e. The chain itself "
    "(depth, reverb, layers) can be tuned later whichever model you choose.",
]

MEASURE_NOTES = [
    "<b>CER.</b> Most remaining errors are the game's invented words: the recogniser writes 盡/淨/進 for 燼, 如/盧 for 爐, 幾主 for 寂主. "
    "The \"sound\" figure forgives homophones. When it writes 極 / 及 / 即 for 寂 it heard the Taiwan reading ㄐㄧˊ; when it writes 寂 it "
    "most likely heard the mainland jì.",
    "<b>寂 with Taiwan tone.</b> Pitch movement inside the syllable 寂 (two in row a, one in row b), in semitones: rising means the Taiwan "
    "reading ㄐㄧˊ was actually spoken, falling means mainland jì. This is a fairly direct check that a model obeys the reading we ask for.",
    "<b>Retroflex index.</b> Spectral centre of the hiss of ㄓㄔㄕ divided by that of ㄗㄘㄙ (2-7.5 kHz). Mainland speech keeps the two "
    "apart (lower value); Taiwanese Mandarin moves them together (value nearer 1). Measured with the same code: Kokoro's mainland voices "
    "0.65-0.70; the real Taiwanese speaker in the bundled clip about 0.82 (only two ㄗㄘㄙ syllables in that clip, so a rough anchor). "
    "It is a weak indicator - a few dozen syllables, 60 ms timing, and pitch-shifting a clip moves these frequencies - good for telling "
    "\"mainland-like\" from \"not\", not for ranking the Taiwanese-prompt columns against each other.",
    "<b>There is no objective \"sounds Taiwanese\" score here.</b> The recogniser tags every sample simply as Chinese. Beyond the two indicators "
    "above, the accent rests on the model's design (BreezyVoice was trained for Taiwanese Mandarin) or on the reference clip - and on your ears.",
    "<b>Speed</b> was measured on the CPU with 6-8 threads while Blender renders were using 11-19 cores, so the figures are conservative "
    "and vary with that load. The whole script (188 lines, about 16 minutes of speech) would take about 2-4 hours on the CPU at these rates. "
    "GPU acceleration has not been attempted yet - by instruction it follows your choice.",
    "<b>Levels.</b> Every file is within 0.5 LU of -17 LUFS and below -1 dBTP, with no clipped samples.",
]

OBSERVED = {
    "kokoro": "Fast (0.2 s per second of audio) and clean, but both 寂 fall (mainland jì) and the consonants measure as mainland "
              "(retroflex index 0.65). This is the gap the request is about.",
    "breezyvoice": "All three 寂 rise as asked, so the model obeys its bopomofo hints when it is not continuing a prompt. But the "
                   "consonants measure the same as Kokoro's (0.67 against 0.65): the mainland-sounding synthetic timbre clip carries its "
                   "colour into the result. The hero line came out slow (2.3 characters per second).",
    "breezyvoice_tw": "The official mode with the official Taiwanese prompt. Consonants move to the Taiwanese side (0.86), but one of the "
                      "three 寂 fell (mainland jì) and the recogniser wrote 寂 there: when the language model continues a prompt it does not "
                      "always obey the bopomofo hint. Slowest column (17 s per second of audio). With the full boss chain the line was "
                      "unintelligible to the recogniser, so the chain was reduced to 70 % for row f.",
    "breezyvoice_tw2": "Taiwanese-side consonants (0.87) and no falling 寂 (the third is nearly flat). Highest raw error rate of the "
                       "BreezyVoice columns: in the narrator line the recogniser lost 持燼者 (it wrote 直徑的) and heard an extra 的.",
    "cosyvoice3": "Obeys its pinyin tokens (all three 寂 rise), has the lowest error among the takes together with VoxCPM2 continuation "
                  "(0.016 with homophones forgiven), and is the fastest of the three large models (8 s per second of audio on 6 threads). "
                  "It warned that the three short lines are shorter than the 8-second prompt; production would use a 4-5 second reference. "
                  "The merchant line came out slower than in the other columns (2.8 characters per second).",
    "voxcpm2_design": "Five voices from five written descriptions, no reference audio. With {pinyin} hints all three 寂 rise; "
                      "<b>without a hint VoxCPM2 read 寂 the mainland way even though the description asks for a Taiwanese accent</b> "
                      "(separate test take: -4.4 and -9.9 semitones), so the description does not change readings - whether it changes the "
                      "sound of the accent is for your ear. Three first-take slips are left in as generated: the hero line starts wrong "
                      "(劍更 heard as 天槓), the elder's first syllable (那 heard as 哇) and the merchant's 幣 (heard as 被). A production run "
                      "would regenerate such lines. Speed: 10-12 s per second of audio last night, 14-16 this morning under heavier load.",
    "voxcpm2_clone": "Cleanest takes of the test (0.011 with homophones forgiven; the hero and merchant lines read back perfectly) at 48 kHz, "
                     "and all three 寂 rise. Accent and timbre come from the Taiwanese clip, as in the CosyVoice 3 column. Speed: 9-10 s per "
                     "second of audio last night, 12-16 this morning under heavier load.",
}

FINDINGS = [
    "<b>Readings can be controlled in all three large models</b>, each with its own syntax: BreezyVoice 寂[:ㄐㄧ2], CosyVoice 3 [j][í], "
    "VoxCPM2 {ji2}. Measured obedience on 寂 (rising tone): CosyVoice 3 3/3; VoxCPM2 3/3 in both modes; BreezyVoice unprompted 3/3 with the "
    "Kokoro timbre and 2/3 with the Taiwanese timbre (the third flat, none falling); BreezyVoice in its official prompt-continuation mode "
    "2/3, with one clearly falling. Left alone, Kokoro and VoxCPM2 say the mainland jì.",
    "<b>The sound of the accent follows the reference clip, even in BreezyVoice.</b> Given a synthetic (Kokoro, mainland-sounding) timbre "
    "clip, BreezyVoice's consonants measure like Kokoro's (0.67 against 0.65); given the Taiwanese clip they move to 0.86-0.87, and "
    "CosyVoice 3 and VoxCPM2 land at 0.79-0.82 with the same clip. So synthetic reference clips made with Kokoro are not enough for a "
    "Taiwanese accent: a reference-driven model needs Taiwanese reference speech, or the accent has to come from somewhere else.",
    "<b>Only one configuration needs no reference at all:</b> VoxCPM2 voice design. Its retroflex index (0.85) is on the Taiwanese side, "
    "but that single number is weak evidence, and its readings are mainland unless hinted. Whether those designed voices pass as "
    "Taiwanese is the main thing to judge by ear.",
    "<b>Intelligibility is good everywhere</b> (mean error with homophones forgiven 0.011-0.052, and no single take above the 0.2 retry "
    "threshold on that measure; counted strictly, two BreezyVoice takes reach 0.21). "
    "After the boss effect chain every column stays under the 0.5 limit, one of them only after the chain was reduced to 70 %.",
    "<b>None of the large models is near real time on this CPU</b> (8-17 s of compute per second of audio, under load). Kokoro needs 0.2 s.",
]

RECOMMENDATION = [
    "<b>My recommendation, from measurements only - I cannot listen:</b> Fun-CosyVoice3 driven by Taiwanese reference clips. Among the "
    "configurations that measurably speak Taiwan readings with Taiwanese-side consonants it is, together with VoxCPM2 continuation, the "
    "most accurate; it is the fastest; it has instruction control for speed and emotion (VoxCPM2 has style hints too, BreezyVoice has "
    "none); and it has a credible GPU route on this machine (its authors publish an ONNX export of the flow decoder, one of its two heavy "
    "parts, and its language model is a standard Qwen2-0.5B). Licence: Apache-2.0 for code and weights.",
    "<b>Choose VoxCPM2 instead if its voice-design column already sounds Taiwanese to you.</b> It would then be the better choice: no "
    "reference audio and nobody's voice involved, as many distinct voices as we write descriptions for, 48 kHz, and community GGUF/Vulkan "
    "ports exist for Radeon graphics. In production each character would be designed once and that clip reused for all of the character's "
    "lines, so a voice cannot drift from line to line.",
    "<b>Choose BreezyVoice only if its columns clearly sound the most Taiwanese to you.</b> It is the one model built for the accent, but "
    "here it is the slowest, the lowest in fidelity (22 kHz), has no style control, and in its official mode missed one reading hint in three.",
    "<b>One decision comes with any reference-driven choice (CosyVoice 3, VoxCPM2 continuation, BreezyVoice): where the 23 reference "
    "voices come from.</b> (a) Keep the single bundled BreezyVoice example, pitch-shifted per role - what you hear now; all roles descend "
    "from one woman's voice. (b) Short clips of different Taiwanese speakers from Mozilla Common Voice zh-TW (CC0), credited - the brief "
    "allows this only if the accent requires real Taiwanese speech, and the second finding above indicates it does. (c) Voices designed "
    "with VoxCPM2, used as reference clips for the chosen model - no real person, but only as Taiwanese as VoxCPM2's design voices are. "
    "Nothing has been downloaded from Common Voice; that waits for your answer.",
]

NOT_DONE = [
    "<b>GPU acceleration: not started</b> (it follows your choice, for the chosen model only). First facts: the Radeon 8060S runs an ASUS "
    "OEM driver, 32.0.22032.6002 (Adrenalin 25.20.32.06, December 2025). AMD's current PyTorch-for-Windows release (ROCm 7.2.1, PyTorch "
    "2.9.1, Python 3.12) lists the Ryzen AI Max+ 395 but documents graphics driver 26.2.2 as required, so that route probably needs a "
    "driver update, which I will not perform. ONNX Runtime with DirectML and Vulkan-based runtimes have no such requirement and will be "
    "tried first.",
    "<b>Only 5 of the 23 voices and 1 of the 7 boss effect chains were sampled.</b> The full 188-line run, per-line validation and delivery "
    "into the game have not been started; <code>Y:\\game\\assets\\audio\\vo</code> is untouched.",
    "<b>The bundled reference clip</b> (BreezyVoice <code>data/example.wav</code>) comes from an Apache-2.0 repository, but the repository "
    "says nothing separate about the recording or its speaker. That is acceptable for this listening test and has to be settled or the "
    "clip replaced before anything ships.",
    "<b>Candidates not sampled.</b> Qwen3-TTS 1.7B (Apache-2.0, voice design; reported to keep a mainland accent and to stumble on some "
    "Traditional characters; another 4.5 GB on a slow link). IndexTTS-2 / 2.5 (bilibili licence: commercial use needs written permission). "
    "F5-TTS (weights CC-BY-NC). Fish-Speech / OpenAudio (non-commercial). Spark-TTS (weights CC-BY-NC-SA). GPT-SoVITS (MIT, but its accent "
    "also comes from the reference, so no advantage here). BreezyVoice-26 (Taiwanese Hokkien, and its weights are not published).",
    "<b>The effect chain in row f</b> is a first port of the fallback tool's profile for 寂主・奧薩洛斯; it has not been tuned per model.",
]
