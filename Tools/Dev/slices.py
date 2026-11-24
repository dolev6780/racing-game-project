import sys, bpy, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
vs = [o.matrix_world @ v.co for o in bpy.data.objects if o.type == "MESH" for v in o.data.vertices]
z0 = min(v.z for v in vs); z1 = max(v.z for v in vs); H = z1 - z0
n = int(a[1]) if len(a) > 1 else 40
print("H", round(H, 3), "x", round(min(v.x for v in vs), 3), round(max(v.x for v in vs), 3), "y", round(min(v.y for v in vs), 3), round(max(v.y for v in vs), 3))
for i in range(n):
    lo = z0 + H * i / n; hi = lo + H / n
    s = [v for v in vs if lo <= v.z < hi]
    if not s: continue
    print(f"{i/n:.3f} x[{min(v.x for v in s):+.2f},{max(v.x for v in s):+.2f}] y[{min(v.y for v in s):+.2f},{max(v.y for v in s):+.2f}] n={len(s)}")
