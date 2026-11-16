# Build game-ready PBR texture sets from the raw Poly Haven / ambientCG downloads.
# run:  blender -b --factory-startup --python Y:\tools\env\tex_process.py
# Uses only OpenImageIO + numpy (bundled with Blender's Python).
# Output: Y:\game\assets\env\tex\<name>\{albedo,normal,arm,height[,emission,opacity]}.jpg + manifest.json
import json, os, sys
import numpy as np
import OpenImageIO as oiio

SPEC = r"Y:\tools\env\tex_spec.json"
PH = r"Y:\assets_src\env\polyhaven\tex"
ACG = r"Y:\assets_src\env\ambientcg"
OUT = r"Y:\game\assets\env\tex"

# estimated physical sizes (metres) for ambientCG sets whose metadata has no dimensions
ACG_SIZE_EST = {"Lava004": 2.0, "Lava001": 2.0, "Rock035": 2.0, "Marble023": 1.5, "Rope001": 0.3, "Wicker010B": 0.4,
                "Snow015": 2.0, "Ice003": 2.0, "Ice004": 2.0, "Moss002": 1.0, "Ground097": 2.0, "Ground093C": 2.0,
                "Metal008": 1.0, "Metal017": 1.0, "Metal048A": 1.0, "Wood067": 1.0, "Carpet015": 0.5, "Tiles074": 1.2, "Tiles131": 1.0}


def read(path):
    buf = oiio.ImageBuf(path)
    if buf.has_error:
        raise RuntimeError(buf.geterror())
    px = buf.get_pixels(oiio.FLOAT)  # H x W x C, 0..1 (display-referred values as stored)
    if px.ndim == 2:
        px = px[:, :, None]
    return np.ascontiguousarray(px)


def resize(px, size):
    h, w, c = px.shape
    if h == size and w == size:
        return px
    src = oiio.ImageBuf(oiio.ImageSpec(w, h, c, oiio.FLOAT))
    src.set_pixels(oiio.ROI(0, w, 0, h), px)
    dst = oiio.ImageBufAlgo.resize(src, roi=oiio.ROI(0, size, 0, size, 0, 1, 0, c))
    return np.ascontiguousarray(dst.get_pixels(oiio.FLOAT)).reshape(size, size, c)


def write_jpg(path, px, quality, subsampling="4:4:4"):
    h, w, c = px.shape
    spec = oiio.ImageSpec(w, h, c, oiio.UINT8)
    spec.attribute("Compression", "jpeg:%d" % quality)
    if c >= 3:
        spec.attribute("jpeg:subsampling", subsampling)
    spec.attribute("oiio:ColorSpace", "sRGB")
    out = oiio.ImageOutput.create(path)
    if not out:
        raise RuntimeError(oiio.geterror())
    out.open(path, spec)
    data = np.clip(px * 255.0 + 0.5, 0, 255).astype(np.uint8)
    ok = out.write_image(data)
    out.close()
    if not ok:
        raise RuntimeError("write failed " + path)


def srgb_to_lin(v):
    return np.where(v <= 0.04045, v / 12.92, ((v + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(v):
    return np.where(v <= 0.0031308, v * 12.92, 1.055 * np.power(v, 1 / 2.4) - 0.055)


def seam_ratio(px):
    """wrap-around edge difference relative to the typical neighbouring-pixel difference (≈1 means seamless)."""
    g = px[:, :, :3].mean(axis=2) if px.shape[2] >= 3 else px[:, :, 0]
    inner_x = np.abs(g[:, 1:] - g[:, :-1]).mean()
    inner_y = np.abs(g[1:, :] - g[:-1, :]).mean()
    wrap_x = np.abs(g[:, 0] - g[:, -1]).mean()
    wrap_y = np.abs(g[0, :] - g[-1, :]).mean()
    return float(max(wrap_x / max(inner_x, 1e-6), wrap_y / max(inner_y, 1e-6)))


def fix_normal(px):
    """Some older scans ship normal maps that were gamma-encoded or carry a constant tilt.
    A tiling flat surface must average to (0,0,1): undo the gamma if the bias is large, remove residual tilt, renormalise."""
    note = []
    mr, mg = float(px[:, :, 0].mean()), float(px[:, :, 1].mean())
    if max(abs(mr - 0.5), abs(mg - 0.5)) > 0.075:
        g = float(np.log(0.5) / np.log((mr + mg) / 2.0))
        px = np.power(np.clip(px, 0, 1), g).astype(np.float32)
        note.append("gamma %.2f removed" % g)
    v = px.astype(np.float32) * 2.0 - 1.0
    mx, my = float(v[:, :, 0].mean()), float(v[:, :, 1].mean())
    if max(abs(mx), abs(my)) > 0.015 or note:
        v[:, :, 0] -= mx
        v[:, :, 1] -= my
        xy2 = np.clip(v[:, :, 0] ** 2 + v[:, :, 1] ** 2, 0, 0.98)
        v[:, :, 2] = np.sqrt(1.0 - xy2)
        s = np.sqrt(np.clip(xy2, 1e-12, None))
        over = (v[:, :, 0] ** 2 + v[:, :, 1] ** 2) > 0.98
        if over.any():
            k = np.sqrt(0.98) / np.sqrt(v[:, :, 0] ** 2 + v[:, :, 1] ** 2 + 1e-12)
            v[:, :, 0] = np.where(over, v[:, :, 0] * k, v[:, :, 0])
            v[:, :, 1] = np.where(over, v[:, :, 1] * k, v[:, :, 1])
        note.append("tilt (%.3f, %.3f) removed, renormalised" % (mx, my))
        px = (v * 0.5 + 0.5).astype(np.float32)
    return px, "; ".join(note)


def first(*paths):
    for p in paths:
        if p and os.path.exists(p):
            return p
    return None


def main():
    spec = json.load(open(SPEC, encoding="utf-8"))
    manifest = []
    total = 0
    old = {}
    mpath = os.path.join(OUT, "manifest.json")
    if os.path.exists(mpath) and "--force" not in sys.argv:
        old = {e["name"]: e for e in json.load(open(mpath, encoding="utf-8"))["sets"]}
    for s in spec:
        name, sid, res = s["name"], s["id"], s["res"]
        size = 2048 if res == "2k" else 1024
        odir = os.path.join(OUT, name)
        os.makedirs(odir, exist_ok=True)
        prev = old.get(name)
        if prev and prev["source_id"] == sid and prev["resolution"] == size and all(os.path.exists(os.path.join(odir, m + ".jpg")) for m in prev["maps"]):
            prev.update(zones=s["zones"], use=s["use"])
            manifest.append(prev)
            total += prev["bytes"]
            continue
        entry = {"name": name, "source": None, "source_id": sid, "resolution": size}
        em = op = None
        if s["src"] == "ph":
            d = os.path.join(PH, sid)
            info = json.load(open(os.path.join(d, "info.json"), encoding="utf-8"))
            alb = first(os.path.join(d, "albedo_%s.jpg" % res))
            nor = first(os.path.join(d, "normal_%s.jpg" % res))
            arm = first(os.path.join(d, "arm_%s.jpg" % res))
            hgt = first(os.path.join(d, "height_%s.jpg" % res))
            ao = first(os.path.join(d, "ao_%s.jpg" % res))
            rough = first(os.path.join(d, "rough_%s.jpg" % res))
            metal = first(os.path.join(d, "metal_%s.jpg" % res))
            dims = info.get("dimensions") or [0, 0]
            size_m = [round(dims[0] / 1000.0, 3), round(dims[1] / 1000.0, 3)]
            size_src = "source metadata"
            entry.update(source="Poly Haven", source_url="https://polyhaven.com/a/" + sid,
                         author=", ".join(info.get("authors", {}).keys()), licence="CC0 1.0")
        else:
            d = os.path.join(ACG, sid, "x")
            pre = os.path.join(d, "%s_%s-JPG_" % (sid, res.upper()))
            info = json.load(open(os.path.join(ACG, sid, "info.json"), encoding="utf-8"))["foundAssets"][0]
            alb, nor, hgt = first(pre + "Color.jpg"), first(pre + "NormalGL.jpg"), first(pre + "Displacement.jpg")
            arm = None
            ao, rough, metal = first(pre + "AmbientOcclusion.jpg"), first(pre + "Roughness.jpg"), first(pre + "Metalness.jpg")
            em, op = first(pre + "Emission.jpg"), first(pre + "Opacity.jpg")
            dx, dy = info.get("dimensionX") or 0, info.get("dimensionY") or 0
            if dx and dy:
                size_m, size_src = [round(dx / 100.0, 3), round(dy / 100.0, 3)], "source metadata"
            else:
                e = ACG_SIZE_EST.get(sid, 2.0)
                size_m, size_src = [e, e], "estimate (source has no dimensions)"
            entry.update(source="ambientCG", source_url="https://ambientcg.com/view?id=" + sid,
                         author="Lennart Demes (ambientCG)", licence="CC0 1.0")
        if not (alb and nor and hgt):
            print("!! missing maps for", name, alb, nor, hgt)
            continue
        maps = []
        # albedo
        a = resize(read(alb)[:, :, :3], size)
        write_jpg(os.path.join(odir, "albedo.jpg"), a, 90, "4:2:0")
        maps.append("albedo")
        lin = srgb_to_lin(a.reshape(-1, 3).astype(np.float64)).mean(axis=0)
        avg = np.clip(lin_to_srgb(lin) * 255 + 0.5, 0, 255).astype(int).tolist()
        seam = seam_ratio(a)
        # normal (OpenGL, +Y up)
        n = resize(read(nor)[:, :, :3], size)
        n, nfix = fix_normal(n)
        write_jpg(os.path.join(odir, "normal.jpg"), n, 92, "4:4:4")
        maps.append("normal")
        gmean = float(n[:, :, 1].mean())
        if nfix:
            entry["normal_fix"] = nfix
        # arm
        if arm:
            m = resize(read(arm)[:, :, :3], size)
            arm_src = "source arm map"
        else:
            m = np.zeros((size, size, 3), np.float32)
            m[:, :, 0] = resize(read(ao), size)[:, :, 0] if ao else 1.0
            m[:, :, 1] = resize(read(rough), size)[:, :, 0] if rough else 0.8
            m[:, :, 2] = resize(read(metal), size)[:, :, 0] if metal else 0.0
            arm_src = "packed from %s" % "+".join(k for k, v in (("AO", ao), ("Roughness", rough), ("Metalness", metal)) if v)
            if not ao:
                arm_src += " (AO=1)"
            if not metal:
                arm_src += " (metal=0)"
        write_jpg(os.path.join(odir, "arm.jpg"), m, 90, "4:4:4")
        maps.append("arm")
        # height
        hpx = resize(read(hgt)[:, :, :1], size)
        write_jpg(os.path.join(odir, "height.jpg"), hpx, 90)
        maps.append("height")
        if em:
            write_jpg(os.path.join(odir, "emission.jpg"), resize(read(em)[:, :, :3], size), 90, "4:2:0")
            maps.append("emission")
        if op:
            write_jpg(os.path.join(odir, "opacity.jpg"), resize(read(op)[:, :, :1], size), 90)
            maps.append("opacity")
        bytes_ = sum(os.path.getsize(os.path.join(odir, f)) for f in os.listdir(odir))
        total += bytes_
        entry.update(size_m=size_m, size_source=size_src,
                     avg_color="#%02x%02x%02x" % tuple(avg), avg_color_rgb=avg,
                     avg_roughness=round(float(m[:, :, 1].mean()), 3), avg_metallic=round(float(m[:, :, 2].mean()), 3),
                     zones=s["zones"], use=s["use"], maps=maps, arm_source=arm_src,
                     normal_convention="OpenGL (+Y)", tiles=True, seam_ratio=round(seam, 2),
                     path="res://assets/env/tex/%s/" % name, bytes=bytes_)
        manifest.append(entry)
        flag = "" if seam < 3.0 else "  <-- CHECK SEAM"
        print("%-18s %-28s %4d  %s  avg %s  rough %.2f  nG %.3f seam %.2f  %.1f MB%s" % (
            name, sid, size, "x".join(str(v) for v in size_m), entry["avg_color"], entry["avg_roughness"], gmean, seam, bytes_ / 1e6, flag))
        sys.stdout.flush()
    doc = {"about": "PBR texture sets for Ashen Abyss. Each set: albedo.jpg (sRGB), normal.jpg (OpenGL), arm.jpg (R=AO,G=roughness,B=metallic, linear), "
                    "height.jpg (linear displacement, mid-grey ~ surface), optional emission.jpg / opacity.jpg. size_m = real-world metres covered by one UV repeat "
                    "(uv_scale = world_metres / size_m).",
           "sets": manifest}
    json.dump(doc, open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print("sets: %d   total %.1f MB" % (len(manifest), total / 1e6))


main()
