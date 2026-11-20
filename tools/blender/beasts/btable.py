"""The table of the 46 creatures (model | family | rig | tris | bones | MB | verify | honest weaknesses) from the verify
results (out\\verify.json, hN\\out\\verify.json) and the weakness notes (the helpers' PROGRESS.md lines + MINE below).
  python.exe -I btable.py   -> out\\table.md"""
import os, json, re
HERE = os.path.dirname(os.path.abspath(__file__))
DIRS = ['out', r'h1\out', r'h2\out', r'h3\out', r'h4\out']
MINE = {
    'mon_dusk_hound': "swarm build (lite skeleton, 1024 atlas); chest / haunch forms simple; mane tufts are flat cones; death pose legs stiff; leap IK stretch 0.17 m",
    'mon_sexton_beetle': "head shield reads lumpy close up; legs thin for a beetle",
    'mon_lurk_eel': "x1.2; no feet, speeds nominal; Attack2 is a low sweep of the body (the tail stays buried); in Death it sinks back into its hole and only a stump and the head stay above ground; mud ripples are geometry rings",
    'mon_dusk_bat': "x1.2; wing membrane stretches at the body on the full upstroke; one-shot clips end in the hover pose, not exactly Idle frame 0",
    'mon_wraith_gasp': "x1.55, swarm build; the ghost face is opaque glowing geometry (only its cowl and smoke trail are ghost matter); the pool of light on the ground is a game light (meta `light`), not part of the model; glass shards opaque",
    'mon_sexton_grub': "x1.25, swarm build, built on the looper (rig C family); bounding Run (each end planted for one frame of eight); stub legs do not move",
    'mon_lurk_leech': "x1.3, swarm build; small and dark on screen; belly rings read like wood grain close up; Run is bounding loops, not a crawl",
    'mon_ash_worm': "Death sinks back into the hole leaving a stump; the sieve holes are glowing discs, not real holes; Walk / Run = leaning weaving column",
    'mon_char_root': "slender; rootlets are cones that move with the body only; no head by brief; does not travel above ground (Walk / Run are a weaving column)",
    'mon_hush_serpent': "plain by brief (one chalk look); the needle-eye is small; Idle / Walk / Run turn the whole corkscrew 360 degrees per loop (unchecked in the engine)",
    'mon_lurk_spitter': "frond tips glow while it is dead; the frill is rigid sheets on six fold bones; Walk / Run frames differ little (tail wave + steady head)",
    'mon_lurk_elder': "giant; flute-holes are painted discs that do not open and close; clinging leeches are static; tail wave is weak on the short fat tail",
}
ver = {}
src = {}
for d in DIRS:
    p = os.path.join(HERE, d, 'verify.json')
    if os.path.isfile(p):
        for k, v in json.load(open(p, encoding='utf8')).items():
            if os.path.isfile(os.path.join(HERE, d, k + '.meta.json')):
                ver[k] = v; src[k] = d
weak = dict(MINE)
for h in ('h1', 'h2', 'h3', 'h4'):
    p = os.path.join(HERE, h, 'PROGRESS.md')
    if os.path.isfile(p):
        for line in open(p, encoding='utf8'):
            if line.startswith('mon_') and line.count('|') >= 7:
                f = [x.strip() for x in line.split('|')]
                weak.setdefault(f[0], "(%s) %s" % (h.upper(), f[7]))
            elif line.startswith('| mon_'):
                f = [x.strip() for x in line.strip().strip('|').split('|')]
                if len(f) >= 8:
                    weak.setdefault(f[0], "(%s) %s" % (h.upper(), f[7]))
EXTRA = {
    'mon_dusk_crow': " Since then: slate feathers with a blue sheen, primaries smoulder at their tips.",
    'mon_rust_locust': " Since then: faint violet static towards the tips of the brass leaves.",
    'mon_sexton_digger': " Since then: paler grave soil and dust so it shows on dark ground.",
}
names = json.load(open(os.path.join(HERE, 'out', 'names.json'), encoding='utf8'))
best = json.load(open(r"Y:\game\data\bestiary.json", encoding='utf8'))
order = [t['model'] for t in best['types'] if t['rig'] in 'BCDEF']
rows = []
for m in order:
    v = ver.get(m, {})
    ok = "PASS" if v and not v.get('fail') else ("FAIL: " + "; ".join(v.get('fail', ['not verified'])))
    rows.append("%s | %s | %s | %s | %s | %s | %s | %s" % (m, names[m]['family'], names[m]['rig'], v.get('tris', '?'), v.get('bones', '?'), v.get('mb', '?'), ok,
                                                      weak.get(m, '') + EXTRA.get(m, '')))
out = os.path.join(HERE, 'out', 'table.md')
open(out, 'w', encoding='utf8').write("\n".join(rows) + "\n")
print("wrote", out, len(rows), "rows;", sum(1 for r in rows if '| PASS |' in r), "PASS; no weakness note:", [m for m in order if m not in weak])
