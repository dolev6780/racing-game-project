# close-up front render of a GLB's middle part: blender -b -P close_render.py -- in.glb out.png
import sys, bpy, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
ms = [o for o in bpy.data.objects if o.type == "MESH"]
print("TRIS", sum(len(o.data.polygons) for o in ms))
pts = [o.matrix_world @ Vector(c) for o in ms for c in o.bound_box]
z0 = min(p.z for p in pts); z1 = max(p.z for p in pts)
sc = bpy.context.scene
sc.render.engine = "CYCLES"; sc.cycles.samples = 16; sc.cycles.device = "CPU"
sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.8, 0.85, 0.9, 1)
l = bpy.data.lights.new("l", "SUN"); l.energy = 3; lo = bpy.data.objects.new("l", l); sc.collection.objects.link(lo); lo.rotation_euler = (math.radians(50), 0, math.radians(-30))
cam = bpy.data.cameras.new("c"); cam.type = "ORTHO"; cam.ortho_scale = (z1 - z0) * 0.45
co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
r = math.radians(35); co.location = (math.sin(r) * 5, -math.cos(r) * 5, z0 + (z1 - z0) * 0.55); co.rotation_euler = (math.radians(90), 0, r)
sc.render.resolution_x, sc.render.resolution_y = 500, 500
sc.render.filepath = a[1]; bpy.ops.render.render(write_still=True)
