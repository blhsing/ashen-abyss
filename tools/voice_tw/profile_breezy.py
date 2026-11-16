"""Where does BreezyVoice spend its time on CPU?

    profile_breezy.py <omp threads> <KMP_BLOCKTIME> <affinity 0|1> [mode] [mkl threads]

Prints seconds per stage and the average number of cores the process kept busy (CPU time / wall time)."""
import os, sys, time
threads, blocktime, aff = int(sys.argv[1]), sys.argv[2], sys.argv[3] == "1"
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import types
cpu_cap = types.SimpleNamespace(THREADS=threads, MASK=0)
if aff:
    import cpu_cap
mkl = sys.argv[5] if len(sys.argv) > 5 else str(threads)
os.environ["OMP_NUM_THREADS"] = str(threads)
os.environ["MKL_NUM_THREADS"] = mkl
os.environ["KMP_BLOCKTIME"] = blocktime
src = open(os.path.join(HERE, "synth_breezy.py"), encoding="utf-8").read().split("def main():")[0].replace("import cpu_cap  # noqa", "pass  # noqa")
exec(compile(src, "synth_breezy_head", "exec"))
torch.set_num_threads(threads)
if mkl != str(threads):
    import ctypes, glob
    for dll in glob.glob(os.path.join(sys.prefix, "Library", "bin", "mkl_rt*.dll")):
        ctypes.CDLL(dll).MKL_Set_Num_Threads(int(mkl))      # torch.set_num_threads also set MKL; override it
        print("MKL threads ->", mkl, os.path.basename(dll))
cosy = bv.CustomCosyVoice(MODEL_DIR)
prompts = Prompts(cosy)
import samples_spec as S
j = dict(ref_wav=os.path.join(S.REFS, "kokoro_merchant.wav"), ref_text=S.REF_TEXT, mode=sys.argv[4] if len(sys.argv) > 4 else "native")
sent = annotate("金幣我收，故事我也收。外頭有什麼消息？")
m = build_input(cosy, prompts, sent, j)
mdl = cosy.model
torch.manual_seed(1)
c0, w0 = time.process_time(), time.time()
with torch.inference_mode():
    t = time.time()
    tok = mdl.llm.inference(text=m["text"], text_len=m["text_len"], prompt_text=m.get("prompt_text", torch.zeros(1, 0, dtype=torch.int32)),
                            prompt_text_len=m.get("prompt_text_len", torch.zeros(1, dtype=torch.int32)),
                            prompt_speech_token=m.get("llm_prompt_speech_token", torch.zeros(1, 0, dtype=torch.int32)),
                            prompt_speech_token_len=m.get("llm_prompt_speech_token_len", torch.zeros(1, dtype=torch.int32)),
                            embedding=m["llm_embedding"], beam_size=1, sampling=25, max_token_text_ratio=30, min_token_text_ratio=3)
    t_llm = time.time() - t; t = time.time()
    mel = mdl.flow.inference(token=tok, token_len=torch.tensor([tok.size(1)], dtype=torch.int32), prompt_token=m["flow_prompt_speech_token"],
                             prompt_token_len=m["flow_prompt_speech_token_len"], prompt_feat=m["prompt_speech_feat"],
                             prompt_feat_len=m["prompt_speech_feat_len"], embedding=m["flow_embedding"])
    t_flow = time.time() - t; t = time.time()
    wav = mdl.hift.inference(mel=mel)
    t_hift = time.time() - t
dur = wav.shape[1] / 22050
cores = (time.process_time() - c0) / (time.time() - w0)
print(f"threads={threads} blocktime={blocktime} affinity={aff} mode={j['mode']}: tokens={tok.size(1)} prompt_frames={m['prompt_speech_feat'].shape[1]} mel={tuple(mel.shape)} audio={dur:.2f}s  llm={t_llm:.1f}s flow={t_flow:.1f}s hift={t_hift:.1f}s  rtf={(t_llm+t_flow+t_hift)/dur:.2f}  cores_busy={cores:.1f} mkl={mkl}", flush=True)
