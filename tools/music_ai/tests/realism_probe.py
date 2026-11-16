"""Weak hint about timbre: zero-shot probabilities (CLAP) for 'real recorded instruments' against 'synthetic
sampled / MIDI instruments', for the procedural and the generated version of each delivered piece.
Runs in the CPU environment (soundfile + scipy + transformers):  python realism_probe.py name[,name...]"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import soundfile as sf
import torch
from clap_score import ClapScorer

LABELS = ["a recording of a real orchestra and real acoustic instruments in a concert hall",
          "a professional film soundtrack recording",
          "synthesized music made with a MIDI keyboard and sampled instruments",
          "low quality computer generated music"]
sc = ClapScorer(device="cpu")
lab = sc.text(LABELS)
print("%-16s %-10s  real  film  midi  lowq" % ("piece", "version"))
for n in sys.argv[1].split(","):
    for ver, path in (("procedural", r"Y:\music_backup\procedural\%s.ogg" % n), ("generated", r"Y:\game\assets\audio\music\%s.ogg" % n)):
        x, sr = sf.read(path, dtype="float32", always_2d=True)
        a = sc.audio(x, sr)
        p = torch.softmax(sc.scale * (a @ lab.T), dim=1).mean(0)
        print("%-16s %-10s  %.2f  %.2f  %.2f  %.2f" % (n, ver, p[0], p[1], p[2], p[3]), flush=True)
