"""Registry of icon builders: id -> (category, function, options)."""
ICONS = {}


def icon(iid, cat="item", **opt):
    def deco(fn):
        ICONS[iid] = (cat, fn, opt)
        return fn
    return deco


def reg(iid, cat, fn, **opt):
    ICONS[iid] = (cat, fn, opt)
