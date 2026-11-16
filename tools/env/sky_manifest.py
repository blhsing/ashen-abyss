# Analyse the .hdr skies (brightest light direction, luminance) and write Y:\game\assets\env\sky\manifest.json
# run: blender -b --factory-startup --python Y:\tools\env\sky_manifest.py
import json, math, os
import numpy as np
import OpenImageIO as oiio

SKY = r"Y:\game\assets\env\sky"
SRC = r"Y:\assets_src\env\polyhaven\hdri"
META = {
    "dusk": (["wastes", "town"], "cold blue-hour dusk, faint warm glow on the horizon: default 灰暮 ashen-dusk sky"),
    "night": (["wastes", "town", "mire"], "moonlit night with thin cloud: night variant, graveyard scenes"),
    "night_dark": (["abyss", "wastes", "crypt"], "very dark overcast night, moon behind cloud: abyss surface / ambient for dark interiors"),
    "cave": (["crypt", "abyss", "sanctum"], "dark brick cellar tunnel (indoor): reflection/ambient probe for crypt and cave interiors"),
    "overcast": (["wastes", "mire", "town", "frost"], "heavy flat grey overcast: neutral gloomy daylight"),
    "snow_overcast": (["frost"], "bright flat overcast over snow: frost pass"),
    "desert_dusk": (["sands"], "clear desert dusk, orange horizon under deep blue: rust-sand ruins"),
    "swamp_fog": (["mire"], "low-contrast fog, sun as a pale disc: swamp"),
}
names = {m["name"]: m["id"] for m in json.load(open(os.path.join(SRC, "map.json"), encoding="utf-8-sig"))}
out = []
for name, sid in names.items():
    p = os.path.join(SKY, name + ".hdr")
    buf = oiio.ImageBuf(p)
    spec = buf.spec()
    px = buf.get_pixels(oiio.FLOAT)[:, :, :3]
    h, w, _ = px.shape
    lum = 0.2126 * px[:, :, 0] + 0.7152 * px[:, :, 1] + 0.0722 * px[:, :, 2]
    # solid-angle weights for an equirect map
    v = (np.arange(h) + 0.5) / h
    sinp = np.sin(v * math.pi)[:, None]
    # brightest region: block-average to 128x64 then argmax (only upper hemisphere + a little below the horizon)
    bh, bw = h // 64, w // 128
    small = lum[:bh * 64, :bw * 128].reshape(64, bh, 128, bw).mean(axis=(1, 3))
    cand = small.copy(); cand[36:, :] = 0
    iy, ix = np.unravel_index(np.argmax(cand), cand.shape)
    u, vv = (ix + 0.5) / 128, (iy + 0.5) / 64
    theta, phi = u * 2 * math.pi, vv * math.pi
    # Godot PanoramaSkyMaterial: u = atan2(dir.x, -dir.z) / 2pi, v = acos(dir.y) / pi
    d = [math.sin(theta) * math.sin(phi), math.cos(phi), -math.cos(theta) * math.sin(phi)]
    avg = px.reshape(-1, 3) * np.repeat(sinp, w, axis=1).reshape(-1, 1)
    avg = avg.sum(axis=0) / (sinp.sum() * w)
    upper = (px[: h // 2] * sinp[: h // 2, :, None]).sum(axis=(0, 1)) / (sinp[: h // 2].sum() * w)
    info = json.load(open(os.path.join(SRC, sid, "info.json"), encoding="utf-8"))
    peak = float(small.max()); mean = float((lum * sinp).sum() / (sinp.sum() * w))
    e = {
        "name": name, "file": name + ".hdr", "path": "res://assets/env/sky/%s.hdr" % name, "source": "Poly Haven", "source_id": sid,
        "source_url": "https://polyhaven.com/a/" + sid, "author": ", ".join(info.get("authors", {}).keys()), "licence": "CC0 1.0",
        "resolution": [spec.width, spec.height], "zones": META[name][0], "use": META[name][1],
        "light_dir_godot": [round(x, 3) for x in d],
        "light_azimuth_deg": round(math.degrees(theta), 1), "light_elevation_deg": round(90 - math.degrees(phi), 1),
        "light_is_distinct": bool(peak > 12 * mean),
        "peak_luminance": round(peak, 3), "mean_luminance": round(mean, 4),
        "avg_color_linear": [round(float(x), 4) for x in avg], "upper_hemisphere_color_linear": [round(float(x), 4) for x in upper],
        "sky_only": "puresky" in sid, "bytes": os.path.getsize(p),
    }
    out.append(e)
    print("%-14s %-34s %dx%d  dir %s az %.0f el %.0f  peak %.2f mean %.4f distinct %s" % (name, sid, spec.width, spec.height, e["light_dir_godot"],
          e["light_azimuth_deg"], e["light_elevation_deg"], peak, mean, e["light_is_distinct"]))
doc = {"about": "2K equirect HDR skies (Radiance .hdr, linear). light_dir_godot = unit vector pointing TOWARD the brightest sky region (sun/moon/glow) for "
                "Godot's PanoramaSkyMaterial mapping (u=0 looks down -Z, +X at u=0.25) with sky rotation 0; aim DirectionalLight3D along the negative of it. "
                "Source metadata has no sun position, so it is measured from the image. mean_luminance helps choose sky energy/exposure "
                "(these are real-world dim skies: expect to multiply energy for night/dusk).",
       "skies": out}
json.dump(doc, open(os.path.join(SKY, "manifest.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
