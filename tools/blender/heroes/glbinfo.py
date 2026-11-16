"""Pure-python GLB/glTF JSON helpers (no bpy needed, but runs fine inside Blender's python)."""
import json, struct, os


def read_gltf_json(path):
    if path.lower().endswith('.gltf'):
        with open(path, 'r', encoding='utf8') as f:
            return json.load(f)
    with open(path, 'rb') as f:
        magic, ver, length = struct.unpack('<III', f.read(12))
        assert magic == 0x46546C67, "not a glb"
        clen, ctype = struct.unpack('<II', f.read(8))
        assert ctype == 0x4E4F534A
        return json.loads(f.read(clen).decode('utf8'))


def skeleton_signature(js):
    """name -> (parent name, translation, rotation, scale) for all joint nodes of the first skin."""
    nodes = js['nodes']
    parent = {}
    for i, n in enumerate(nodes):
        for c in n.get('children', []):
            parent[c] = i
    joints = js['skins'][0]['joints']
    sig = {}
    for j in joints:
        n = nodes[j]
        p = parent.get(j)
        sig[n['name']] = (
            nodes[p]['name'] if p is not None else None,
            tuple(n.get('translation', (0, 0, 0))),
            tuple(n.get('rotation', (0, 0, 0, 1))),
            tuple(n.get('scale', (1, 1, 1))),
        )
    return sig


def compare_skeletons(a, b, tol=1e-4):
    """returns list of differences between two signatures"""
    diffs = []
    for k in a:
        if k not in b:
            diffs.append("missing bone " + k)
            continue
        pa, ta, ra, sa = a[k]
        pb, tb, rb, sb = b[k]
        if k != 'root' and pa != pb and not (pa not in a and pb not in b):
            diffs.append("parent differs %s: %s vs %s" % (k, pa, pb))
        if max(abs(x - y) for x, y in zip(ta, tb)) > tol:
            diffs.append("translation differs %s %s %s" % (k, ta, tb))
        # quaternion sign ambiguity
        d1 = max(abs(x - y) for x, y in zip(ra, rb))
        d2 = max(abs(x + y) for x, y in zip(ra, rb))
        if min(d1, d2) > tol:
            diffs.append("rotation differs %s %s %s" % (k, ra, rb))
        if max(abs(x - y) for x, y in zip(sa, sb)) > tol:
            diffs.append("scale differs %s %s %s" % (k, sa, sb))
    for k in b:
        if k not in a:
            diffs.append("extra bone " + k)
    return diffs
