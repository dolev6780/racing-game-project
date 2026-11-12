"""
Splits the faces of a one-mesh model above a height into a second object with its origin at a pivot, so the game can
turn that part on its own (the lever's handle, thrown across instead of jumping to the other side: the user).

    python split_part.py -- IN.fbx OUT.fbx PART ZCUT PX PY PZ

Faces whose middle is above ZCUT (in the model's own Blender space, Z up) go to a new object named PART, its origin at
(PX, PY, PZ); the rest stays in the first object. Exported as the props are (Tools/Blender/meshy_prop.py).
"""

import sys

import bpy
import bmesh  # (after bpy: the bpy module provides it)
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, out, part = args[0], args[1], args[2]
zcut = float(args[3])
pivot = Vector([float(a) for a in args[4:7]])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
body = next(o for o in bpy.data.objects if o.type == "MESH")
bpy.context.view_layer.objects.active = body
body.select_set(True)

bpy.ops.object.mode_set(mode="EDIT")
bm = bmesh.from_edit_mesh(body.data)
bpy.ops.mesh.select_all(action="DESELECT")
count = 0
for f in bm.faces:
    if f.calc_center_median().z > zcut:
        f.select = True
        count += 1
bmesh.update_edit_mesh(body.data)
bpy.ops.mesh.separate(type="SELECTED")
bpy.ops.object.mode_set(mode="OBJECT")

piece = next(o for o in bpy.data.objects if o.type == "MESH" and o is not body)
piece.name = part
piece.data.name = part
# its origin at the pivot (world space: the import left the body unrotated at the origin)
bpy.context.scene.cursor.location = body.matrix_world @ pivot
bpy.ops.object.select_all(action="DESELECT")
piece.select_set(True)
bpy.context.view_layer.objects.active = piece
bpy.ops.object.origin_set(type="ORIGIN_CURSOR")

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"}, bake_anim=False,
                         apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP")
print(f"wrote {out}: {count} faces moved to {part}, origin {tuple(round(v, 3) for v in piece.location)}")
