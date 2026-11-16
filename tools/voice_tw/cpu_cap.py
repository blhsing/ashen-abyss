"""CPU budget for inference: keep the process within 8 cores. Import BEFORE torch / numpy.

Measured with BreezyVoice (PyTorch 2.3.1+cpu on Windows, a 4.1 s line, Blender rendering in parallel;
"cores" = process CPU time / wall time, see profile_breezy.py):

    OMP threads  KMP_BLOCKTIME   RTF    cores kept busy
        8          200 (default)  9.0      19.7     <- two OpenMP runtimes are loaded (torch\\lib and the
        8          1              9.7      16.0        intel-openmp wheel that mkl depends on); both pools
        8          0             10.9      10.0        spin while idle
        4          200           11.7      11.7
        8 pinned to 8 cores, 0   23.2       2.0     (pinned cores were shared with Blender)
        1          200           25.1       1.0

The model is not thread-hungry (the language model takes ~80 ms per token whatever the setting), so
spinning buys almost nothing. Settings used: KMP_BLOCKTIME=0 (idle workers sleep at once) and
THREADS such that the observed load stays at or under 8 cores: 6 for the torch-2.3 environments
(breezy, cosy), 8 elsewhere. Override with VT_THREADS; VT_PIN=1 adds a hard affinity mask.
"""
import ctypes
import os

THREADS = int(os.environ.get("VT_THREADS", "8"))
FIRST_CORE = 8          # with VT_PIN=1: physical cores 8..15 (logical 16, 18, ... 30)

for _k in ("OMP_NUM_THREADS", "MKL_NUM_THREADS", "OPENBLAS_NUM_THREADS", "NUMEXPR_NUM_THREADS", "VECLIB_MAXIMUM_THREADS"):
    os.environ[_k] = str(THREADS)
os.environ.setdefault("KMP_BLOCKTIME", "0")
os.environ["MKL_DYNAMIC"] = "FALSE"


def apply() -> int:
    """Pin this process to 8 logical CPUs, one per physical core; returns the mask (0 = not pinned)."""
    if os.environ.get("VT_PIN") != "1" or os.name != "nt":
        return 0
    n = os.cpu_count() or 1
    cpus = [2 * (FIRST_CORE + i) for i in range(min(THREADS, 8))]
    if max(cpus) >= n:
        cpus = [c for c in range(0, n, 2)][:THREADS] or list(range(min(n, THREADS)))
    mask = 0
    for c in cpus:
        mask |= 1 << c
    k32 = ctypes.WinDLL("kernel32", use_last_error=True)
    k32.GetCurrentProcess.restype = ctypes.c_void_p
    k32.SetProcessAffinityMask.argtypes = [ctypes.c_void_p, ctypes.c_size_t]
    return mask if k32.SetProcessAffinityMask(k32.GetCurrentProcess(), mask) else 0


MASK = apply()
