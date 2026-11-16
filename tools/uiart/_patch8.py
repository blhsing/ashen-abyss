p = r"Y:\tools\uiart\pieces.py"
s = open(p, encoding="utf-8").read()
def rep(a, b, n=1):
    global s
    assert s.count(a) == n, (s.count(a), a[:90])
    s = s.replace(a, b)
rep("    Ro = 181.0                                             # outer radius; the glass sits inside ~150",
    "    Ro = 176.0                                             # outer radius; inner lip at 142 covers the edge of the glass (r = 144.5)")
rep("        ray(k, (c, c), math.radians(-90 + i * 17.0), Ro - 6, Ro + (34.0 if i == 0 else 22.0), 0.060 if i == 0 else 0.048)",
    "        ray(k, (c, c), math.radians(-90 + i * 15.0), Ro - 6, Ro + (18.0 if i == 0 else 11.0), 0.060 if i == 0 else 0.048)")
rep("    Ro = 246.0                                             # the map disc (r = 236) ends under the inner lip",
    "    Ro = 240.0                                             # inner lip at 210 covers the edge of the map disc (r = 212.4)")
open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
