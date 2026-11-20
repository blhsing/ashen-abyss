"""round 6 (2026-11-20): assembler gets `sway` (hanging rigid pieces lag behind their bone) and `bind_w` (late weight
merge: a limb is posed with its own bones, then bound to another bone for the animation).  Applied once; do not re-run."""
p = 'Y:/tools/blender/monsters/asm.py'
s = open(p, encoding='utf-8').read()


def rep(a, b):
    global s
    assert a in s, a[:100]
    s = s.replace(a, b, 1)


rep("""        self.merge = {}            # bone -> bone it is merged into (stiff limbs, see body_flesh merge_w)
""",
    """        self.merge = {}            # bone -> bone it is merged into (stiff limbs, see body_flesh merge_w)
        self.late = {}             # bone -> bone it is bound to AFTER the sculpt pose (body_flesh bind_w)
""")
rep("""    def add(self, part, bone=None, look=None, obj=None, smooth=None, mirror=False, flip=False, final=False, skin=None,
            ride=None, like=None, noground=False, noscale=False, keep=False):""",
    """    def add(self, part, bone=None, look=None, obj=None, smooth=None, mirror=False, flip=False, final=False, skin=None,
            ride=None, like=None, noground=False, noscale=False, keep=False, sway=None):""")
rep("""            p._keep_c = None if isinstance(keep, bool) or keep is None else np.asarray(keep, float)
        return part""",
    """            p._keep_c = None if isinstance(keep, bool) or keep is None else np.asarray(keep, float)
            # sway = (low bone, t): a rigid piece that HANGS from its bone.  t (per vertex, 0..1, from rest coordinates) is the
            # share of `low bone` (normally 'pelvis', which tips far less than the chest in the attack clips): after the piece
            # has been placed like any rigid part it becomes skinned  (1 - t) x its bone + t x low bone,  so its lower end lags
            # and stays nearer the vertical when the trunk tips.  No extra bones, the shared skeleton is untouched.
            p._sway = None
            if sway is not None and bone is not None:
                t = np.clip(np.broadcast_to(np.asarray(sway[1], float), (len(p.v),)), 0.0, 1.0).copy()
                p._sway = (sway[0], t)
        return part""")
rep("""                M.pose_part(p, full, p.bone)
        self._fit_budget()""",
    """                M.pose_part(p, full, p.bone)
                sw = getattr(p, '_sway', None)
                if sw is not None:                  # hanging piece: rigid placement done, now it lags by a weight blend
                    W = M.rigid_W(len(p.v), p.bone) * (1.0 - sw[1])[:, None].astype(np.float32)
                    W[:, R.idx[sw[0]]] += sw[1].astype(np.float32)
                    p.W = W; p.bone = None
        if self.late:                           # limbs posed with their own bones, animated by another one (bind_w)
            for p in C.parts:
                if p.bone is not None:
                    p.bone = self.late.get(p.bone, p.bone)
                elif p.W is not None:
                    for sb, tgt in self.late.items():
                        i, t = R.idx[sb], R.idx[tgt]
                        if i != t and p.W[:, i].any():
                            p.W[:, t] += p.W[:, i]; p.W[:, i] = 0
        self._fit_budget()""")
rep("""    merge_w = {bone: [bones / regions]} (their weights go to `bone`: a stiff limb)\"\"\"""",
    """    merge_w = {bone: [bones / regions]} (their weights go to `bone`: a stiff limb),
    bind_w = {bone: [bones / regions]} (the same, but AFTER the sculpt pose: the limb is posed by its own bones -- props
    pose -- and then animated by `bone`: an arm that holds on to a load which is rigid on that bone)\"\"\"""")
rep("""    # merge_w = {target bone: [bones / regions]}: limbs that were baked far off their joints""",
    """    for tgt, srcs in b.get('bind_w', {}).items():
        for sb in region_bones(srcs):
            A.late[sb] = tgt
    # merge_w = {target bone: [bones / regions]}: limbs that were baked far off their joints""")
rep("""    budget   triangle budget (default 15000; giants 22000)""",
    """    budget   triangle budget (default 15000; giants 22000)
    (parts: A.add(..., sway=(low bone, t)) makes a rigid piece hang -- see add();  body bind_w -- see body_flesh)""")
open(p, 'w', encoding='utf-8', newline='\n').write(s)
import ast
ast.parse(s)
print('asm round6 ok')
