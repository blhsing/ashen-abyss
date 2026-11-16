"""Stand-ins for packages that the official repos import at module level but never call at inference
time on our path (dataset readers, downloaders, text normalisers that fetch models, Linux-only tools).
Registering them avoids ~100 MB of downloads and, for pynini/pyworld, a C++ build on Windows.
A stub is only installed when the real package is missing."""
import importlib.util
import sys
import types


def _missing(name: str) -> bool:
    try:
        return importlib.util.find_spec(name) is None
    except (ImportError, ValueError):
        return True


def install(*names: str) -> None:
    for name in names:
        root = name.split(".")[0]
        if not _missing(root):
            continue
        parts = name.split(".")
        for i in range(1, len(parts) + 1):
            mod = ".".join(parts[:i])
            if mod not in sys.modules:
                m = types.ModuleType(mod)
                m.__path__ = []          # looks like a package, so submodule imports resolve to our stubs
                sys.modules[mod] = m
                if i > 1:
                    setattr(sys.modules[".".join(parts[:i - 1])], parts[i - 1], m)


class PassThroughNormalizer:
    """Replacement for WeTextProcessing / wetext normalisers: our lines contain no digits or symbols."""

    def __init__(self, *a, **k):
        pass

    def normalize(self, text):
        return text
