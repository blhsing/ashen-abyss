# Verify font files: sfnt magic, tables, static/variable, family/style names, weight class and cmap coverage.
# Pure Python (no fontTools). run: python -I font_check.py <design.md> <out.json> <font> [<font> ...]
import json, struct, sys

REQUIRED = "燼淵寂滅鑄靈魂"
PUNCT = "，。、・「」『』：；！？…—（）《》％＋"


def tables(data):
    magic = data[:4]
    num = struct.unpack(">H", data[4:6])[0]
    t = {}
    for i in range(num):
        tag, _cs, off, ln = struct.unpack(">4sIII", data[12 + 16 * i: 28 + 16 * i])
        t[tag.decode("latin1")] = (off, ln)
    return magic, t


def cmap_codepoints(data, off):
    ver, n = struct.unpack(">HH", data[off:off + 4])
    subs = []
    for i in range(n):
        pid, eid, so = struct.unpack(">HHI", data[off + 4 + 8 * i: off + 12 + 8 * i])
        fmt = struct.unpack(">H", data[off + so: off + so + 2])[0]
        subs.append((pid, eid, fmt, off + so))
    cps = set()
    for pid, eid, fmt, o in subs:
        if fmt == 12:
            ngroups = struct.unpack(">I", data[o + 12:o + 16])[0]
            for g in range(ngroups):
                s, e, gid = struct.unpack(">III", data[o + 16 + 12 * g: o + 28 + 12 * g])
                if gid != 0 or e > s:
                    cps.update(range(s, e + 1))
        elif fmt == 4:
            segx2 = struct.unpack(">H", data[o + 6:o + 8])[0]
            seg = segx2 // 2
            ends = struct.unpack(">%dH" % seg, data[o + 14:o + 14 + segx2])
            p = o + 14 + segx2 + 2
            starts = struct.unpack(">%dH" % seg, data[p:p + segx2]); p += segx2
            deltas = struct.unpack(">%dh" % seg, data[p:p + segx2]); p += segx2
            ro_pos = p
            ros = struct.unpack(">%dH" % seg, data[p:p + segx2])
            for k in range(seg):
                s, e = starts[k], ends[k]
                if s == 0xFFFF:
                    continue
                for c in range(s, e + 1):
                    if ros[k] == 0:
                        gid = (c + deltas[k]) & 0xFFFF
                    else:
                        gp = ro_pos + 2 * k + ros[k] + 2 * (c - s)
                        gid = struct.unpack(">H", data[gp:gp + 2])[0]
                        if gid:
                            gid = (gid + deltas[k]) & 0xFFFF
                    if gid:
                        cps.add(c)
    return cps, sorted(set((p, e, f) for p, e, f, _ in subs))


def names(data, off):
    _fmt, count, so = struct.unpack(">HHH", data[off:off + 6])
    out = {}
    for i in range(count):
        pid, eid, lid, nid, ln, no = struct.unpack(">6H", data[off + 6 + 12 * i: off + 18 + 12 * i])
        if nid in (1, 2, 4, 13, 16, 17) and pid == 3 and lid == 0x409:
            out[nid] = data[off + so + no: off + so + no + ln].decode("utf-16-be", "replace")
    return out


def main():
    design, outp, fonts = sys.argv[1], sys.argv[2], sys.argv[3:]
    text = open(design, encoding="utf-8").read()
    design_cjk = sorted(set(c for c in text if 0x3400 <= ord(c) <= 0x9FFF))
    report = []
    for f in fonts:
        data = open(f, "rb").read()
        magic, t = tables(data)
        kind = {b"OTTO": "OpenType/CFF", b"\x00\x01\x00\x00": "TrueType", b"true": "TrueType"}.get(magic, "UNKNOWN " + repr(magic))
        cps, subs = cmap_codepoints(data, t["cmap"][0])
        nm = names(data, t["name"][0])
        wght = struct.unpack(">H", data[t["OS/2"][0] + 4: t["OS/2"][0] + 6])[0] if "OS/2" in t else None
        cjk = sum(1 for c in cps if 0x4E00 <= c <= 0x9FFF)
        miss_req = [c for c in REQUIRED if ord(c) not in cps]
        miss_design = [c for c in design_cjk if ord(c) not in cps]
        miss_punct = [c for c in PUNCT if ord(c) not in cps]
        latin = all(ord(c) in cps for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
        r = {"file": f.replace("\\", "/").split("/")[-1], "bytes": len(data), "format": kind, "variable": "fvar" in t,
             "family": nm.get(16) or nm.get(1), "style": nm.get(17) or nm.get(2), "weight_class": wght,
             "codepoints": len(cps), "cjk_unified": cjk, "cmap_subtables": ["p%d/e%d/f%d" % s for s in subs],
             "required_missing": miss_req, "design_md_cjk": len(design_cjk), "design_md_missing": miss_design,
             "punct_missing": miss_punct, "latin_digits_ok": latin, "licence": (nm.get(13) or "")[:90]}
        report.append(r)
        print("%-26s %-13s var=%-5s %-16s %-8s w%s  cps %5d  CJK %5d  req-missing %s  design-missing %d/%d  punct-missing %s  latin %s" % (
            r["file"], kind, r["variable"], r["family"], r["style"], wght, len(cps), cjk, len(miss_req), len(miss_design), len(design_cjk),
            len(miss_punct), latin))
    json.dump(report, open(outp, "w", encoding="utf-8"), indent=1, ensure_ascii=False)


main()
