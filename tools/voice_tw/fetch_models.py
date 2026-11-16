"""Download official model weights from Hugging Face into %VT_ROOT%\\models (anonymous, resumable).

    python fetch_models.py breezy cosy3 voxcpm2 qwen_vd ...

Only the files needed for inference are fetched (no TensorRT exports, no Linux-only ttsfrd data).
"""
import os
import sys
import time

from huggingface_hub import snapshot_download

ROOT = os.environ.get("VT_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "voice_tw"))

MODELS = {
    # MediaTek Research BreezyVoice (Apache-2.0): CosyVoice-300M fine-tuned for Taiwanese Mandarin
    "breezy": dict(
        repo="MediaTek-Research/BreezyVoice",
        allow=["llm.pt", "flow.pt", "hift.pt", "campplus.onnx", "speech_tokenizer_v1.onnx", "*.yaml", "*.md", "spk2info.pt", "*.json"],
    ),
    # FunAudioLLM Fun-CosyVoice3-0.5B-2512 (Apache-2.0)
    "cosy3": dict(
        repo="FunAudioLLM/Fun-CosyVoice3-0.5B-2512",
        allow=["llm.pt", "flow.pt", "hift.pt", "campplus.onnx", "speech_tokenizer_v3.onnx", "*.yaml", "*.md",
               "CosyVoice-BlankEN/*.json", "CosyVoice-BlankEN/*.txt", "*.json"],
    ),
    # OpenBMB VoxCPM2 (Apache-2.0)
    "voxcpm2": dict(repo="openbmb/VoxCPM2", allow=None),
    # Alibaba Qwen3-TTS 1.7B VoiceDesign (Apache-2.0)
    "qwen_vd": dict(repo="Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign", allow=None),
}


def main():
    for name in sys.argv[1:]:
        m = MODELS[name]
        dest = os.path.join(ROOT, "models", name)
        t0 = time.time()
        print(f"== {name}: {m['repo']} -> {dest}", flush=True)
        snapshot_download(m["repo"], local_dir=dest, allow_patterns=m["allow"], token=False, max_workers=4)
        size = sum(os.path.getsize(os.path.join(d, f)) for d, _, fs in os.walk(dest) for f in fs)
        print(f"== {name}: done, {size / 1e9:.2f} GB in {time.time() - t0:.0f} s", flush=True)


if __name__ == "__main__":
    main()
