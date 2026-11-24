# Renders a GLB from front, side and 3/4 into one row: blender -b -P glb_views.py -- in.glb out.png
import sys, bpy, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
ms = [o for o in bpy.data.objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in ms for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
c = (lo + hi) / 2; size = max(hi - lo)
sc = bpy.context.scene
sc.render.engine = "CYCLES"; sc.cycles.samples = 16; sc.cycles.device = "CPU"
sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.8, 0.85, 0.9, 1); sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
l = bpy.data.lights.new("l", "SUN"); l.energy = 3; lo_ = bpy.data.objects.new("l", l); sc.collection.objects.link(lo_); lo_.rotation_euler = (math.radians(50), 0, math.radians(-30))
cam = bpy.data.cameras.new("c"); cam.type = "ORTHO"; cam.ortho_scale = size * 1.1
co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
sc.render.resolution_x, sc.render.resolution_y = 400, 560
import os
outs = []
for i, ang in enumerate((0, 90, 35)):
    r = math.radians(ang); d = size * 3
    co.location = (c.x + math.sin(r) * d, c.y - math.cos(r) * d, c.z)
    co.rotation_euler = (math.radians(90), 0, r)
    f = a[1] + f"_{i}.png"; sc.render.filepath = f; bpy.ops.render.render(write_still=True); outs.append(f)
print("DIMS", tuple(round(v, 3) for v in (hi - lo)))
