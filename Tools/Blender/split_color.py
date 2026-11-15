"""
Splits the faces of a one-mesh model by their colour into an object of its own, its origin at a pivot, so the game
can move that part by itself (the fire crystal lifted out of its stone paws: the crystal is red and gold, the paws
grey, and the crystal's foot sits between the paws, so no box could separate them).

    python split_color.py -- IN.fbx ALBEDO.png OUT.fbx PART MIN_SATURATION HUE_LO HUE_HI PX PY PZ

A face goes to PART when the albedo at its UVs' middle has a saturation of at least MIN_SATURATION and a hue (0..1)
between HUE_LO and HUE_HI (wrapping round 1 when HUE_LO > HUE_HI: the reds); the rest stays in the first object.
The pivot is in the model's own Blender space (Z up). Exported as the props are (Tools/Blender/meshy_prop.py).
"""

import colorsys
import sys

import bpy
import bmesh  # (after bpy: the bpy module provides it)
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, albedo, out, part = args[0], args[1], args[2], args[3]
min_sat, hue_lo, hue_hi = float(args[4]), float(args[5]), float(args[6])
pivot = Vector((float(args[7]), float(args[8]), float(args[9])))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
body = next(o for o in bpy.data.objects if o.type == "MESH")
img = bpy.data.images.load(albedo)
w, h = img.size
px = list(img.pixels)


def colour(u, v):
    x = min(w - 1, max(0, int((u % 1.0) * w)))
    y = min(h - 1, max(0, int((v % 1.0) * h)))
    i = (y * w + x) * 4
    return px[i], px[i + 1], px[i + 2]


def wanted(r, g, b):
    hue, _, sat = colorsys.rgb_to_hls(r, g, b)
    if sat < min_sat or max(r, g, b) < 0.2:
        return False
    return hue_lo <= hue <= hue_hi if hue_lo <= hue_hi else hue >= hue_lo or hue <= hue_hi


bm = bmesh.new()
bm.from_mesh(body.data)
uv = bm.loops.layers.uv.active
take = []
for f in bm.faces:
    us = [l[uv].uv for l in f.loops]
    u = sum(c.x for c in us) / len(us)
    v = sum(c.y for c in us) / len(us)
    if wanted(*colour(u, v)):
        take.append(f.index)
bm.free()
print(f"{part}: {len(take)} of {len(body.data.polygons)} faces")

# the chosen faces into their own object
bpy.context.view_layer.objects.active = body
body.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="DESELECT")
bpy.ops.object.mode_set(mode="OBJECT")
for i in take:
    body.data.polygons[i].select = True
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.separate(type="SELECTED")
bpy.ops.object.mode_set(mode="OBJECT")
piece = next(o for o in bpy.data.objects if o.type == "MESH" and o != body)
piece.name = part
piece.data.name = part
# its origin at the pivot
bpy.context.scene.cursor.location = pivot
bpy.ops.object.select_all(action="DESELECT")
piece.select_set(True)
bpy.context.view_layer.objects.active = piece
bpy.ops.object.origin_set(type="ORIGIN_CURSOR")

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"}, bake_anim=False,
                         apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP")
print("wrote", out)
