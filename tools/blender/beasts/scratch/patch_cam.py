import os, ast
HERE = r"Y:\tools\blender\beasts"
p = os.path.join(HERE, 'bshots.py')
s = open(p, encoding='utf8').read()
a = """        # the game camera (pitch 52 deg, 24 m, FOV 30 deg at 1080p = 84 px per metre) on dark ground in dim light: this
        # 400 px tile shows 4.76 m, so the creature has the pixel size it has on a 1080p screen
        sc.render.resolution_x = sc.render.resolution_y = 400
        dark(True, ck)
        ST.aim(cam, (0, 0, 0.75), 25.0, 52.0, 24.0, lens=181.5)"""
b = """        # the game camera (CameraRig.cs: pitch 48 deg, 15.6 m at zoom 1, vertical FOV 32 deg -> 121 px per metre at 1080p)
        # on dark ground in dim light: this 400 px tile shows 3.31 m, so the creature has its real pixel size on screen
        sc.render.resolution_x = sc.render.resolution_y = 400
        dark(True, ck)
        ST.aim(cam, (0, 0, 0.75), 25.0, 48.0, 15.6, lens=169.5)"""
assert a in s
s = s.replace(a, b)
s = s.replace("on dark ground (52 deg down, 24 m; 400 px = 4.76 m)", "on dark ground (48 deg down, 15.6 m; 400 px = 3.31 m)")
ast.parse(s)
open(p + '.new', 'w', encoding='utf8').write(s); os.replace(p + '.new', p)
p = os.path.join(HERE, 'HELPERS.md')
s = open(p, encoding='utf8').read()
s = s.replace("top-down camera: pitch 52 deg,\n24 m away - a 1 m creature is about 85 px tall on a 1080p screen", "top-down camera: pitch 48 deg,\n15.6 m away at zoom 1 - a 1 m creature is about 120 px tall on a 1080p screen")
s = s.replace("- Judge `<model>_game.png` (512 px = 6.1 m, the real size on a 1080p screen)", "- Judge `<model>_game.png` (400 px = 3.3 m on dark ground, the real size on a 1080p screen)")
open(p + '.new', 'w', encoding='utf8').write(s); os.replace(p + '.new', p)
print('ok')
