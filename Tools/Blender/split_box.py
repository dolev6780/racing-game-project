"""
Splits the faces of a one-mesh model lying inside boxes into objects of their own, each with its origin at a pivot,
so the game can turn those parts on their own (the saw drone's blade and propeller spin).

    python split_box.py -- IN.fbx OUT.fbx PART X0 X1 Y0 Y1 Z0 Z1 PX PY PZ [PART ... ]

Faces whose middle lies inside X0..X1, Y0..Y1, Z0..Z1 (the model's own Blender space, Z up) go to a new object named
PART, its origin at (PX, PY, PZ); the rest stays in the first object. Exported as the props are
(Tools/Blender/meshy_prop.py).
"""

import sys

import bpy
import bmesh  # (after bpy: the bpy module provides it)
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, out, specs = args[0], args[1], args[2:]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
body = next(o for o in bpy.data.objects if o.type == "MESH")

for k in range(0, len(specs), 10):
    part = specs[k]
    x0, x1, y0, y1, z0, z1, px, py, pz = (float(v) for v in specs[k + 1:k + 10])
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(body.data)
    bpy.ops.mesh.select_all(action="DESELECT")
    count = 0
    for f in bm.faces:
        c = body.matrix_world @ f.calc_center_median()
        if x0 <= c.x <= x1 and y0 <= c.y <= y1 and z0 <= c.z <= z1:
            f.select = True
            count += 1
    bmesh.update_edit_mesh(body.data)
    before = set(bpy.data.objects)
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    piece = next(o for o in bpy.data.objects if o not in before)
    piece.name = part
    piece.data.name = part
    bpy.context.scene.cursor.location = Vector((px, py, pz))
    bpy.ops.object.select_all(action="DESELECT")
    piece.select_set(True)
    bpy.context.view_layer.objects.active = piece
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    print(f"{count} faces moved to {part}, origin {(px, py, pz)}")

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"}, bake_anim=False,
                         apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP")
print(f"wrote {out}")
