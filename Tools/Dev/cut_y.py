# Cuts a GLB at y = CUT (Blender units), keeping the -y side, fills the cut, writes a GLB.
import sys, bpy, bmesh
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a[0])
cut = float(a[2])
for o in [o for o in bpy.data.objects if o.type == "MESH"]:
    o.data.transform(o.matrix_world); o.matrix_world.identity()
    bm = bmesh.new(); bm.from_mesh(o.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, cut, 0), plane_no=(0, 1, 0), clear_outer=True)
    bm.to_mesh(o.data); bm.free()
bpy.ops.export_scene.gltf(filepath=a[1], export_format="GLB")
print("cut at", cut)
