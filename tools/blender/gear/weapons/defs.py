"""Registry of the gear bases.

DEFS[base] = dict(fn=build function (w, v), info=manifest defaults, texel=, prefix=, sheet=, ...)
A variant may live inside the base function (if v == 2: ...) or be registered separately with @variant(base, v);
the separate one wins. defs_1h / defs_2h / defs_off hold the bases with their forged (v2) designs, defs_v1 the crude
variants, defs_v3 the ornate / glowing ones, defs_jewel rings, amulets and gems.
"""
DEFS = {}
VARS = {}


def base(name, **opts):
    def deco(fn):
        def dispatch(w, v, _fn=fn, _name=name):
            if (_name, v) in VARS:
                return VARS[(_name, v)](w)
            return _fn(w, v)
        DEFS[name] = dict(fn=dispatch, **opts)
        return fn
    return deco


def variant(name, v):
    def deco(fn):
        VARS[(name, v)] = fn
        return fn
    return deco


import defs_1h      # noqa: E402  (registers its bases)
for _m in ("defs_2h", "defs_off", "defs_v1", "defs_v3", "defs_jewel"):
    try:
        __import__(_m)
    except ModuleNotFoundError as e:
        if e.name != _m:
            raise
