# Slices a GLB by height after turning it phi degrees about Z (as the game will show it), screen x = rotated x.
import sys, bpy, math
a = sys.argv[sys.argv.index("--") + 1:]
phi = math.radians(float(a[1])); n = int(a[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
vs = [o.matrix_world @ v.co for o in bpy.data.objects if o.type == "MESH" for v in o.data.vertices]
c, s = math.cos(phi), math.sin(phi)
pts = [(v.x * c - v.y * s, v.x * s + v.y * c, v.z) for v in vs]
z0 = min(p[2] for p in pts); H = max(p[2] for p in pts) - z0
xs = [p[0] for p in pts]; print("H", round(H, 3), "x", round(min(xs), 3), round(max(xs), 3))
for i in range(n):
    lo = z0 + H * i / n; hi = lo + H / n
    q = [p for p in pts if lo <= p[2] < hi]
    if q: print(f"{(i+1)/n:.3f} x[{min(p[0] for p in q)/H:+.3f},{max(p[0] for p in q)/H:+.3f}] y[{min(p[1] for p in q)/H:+.3f},{max(p[1] for p in q)/H:+.3f}]")
