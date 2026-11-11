"""
Cuts the rope bridge model (Resources/Cloudborne/Jungle/RopeBridge/RopeBridge.fbx, 7.2 m long along X) into a kit
the game strings into a bridge of any length: an end with its bamboo posts, and a two-metre section of planks
from the middle (its deck), so a stretch of deck can be left out. The cuts fall in the gaps between planks, so the
sections join without a seam. The game makes the ropes between the ends itself, each one curve over the whole
bridge (the model's rails, cut into sections and chained, kinked at every joint).

    blender --background --python Tools/Blender/bridge_kit.py -- RopeBridge.fbx OUT_DIR

Writes OUT_DIR/BridgeEnd.fbx (the end, from the cut at x = 0 out to -x) and BridgeDeck.fbx (from x = 0 to
+SECTION), in metres, the deck's top near z = DECK_TOP.
"""

import os
import sys

import bpy
import bmesh  # after bpy: bpy loads it

args = sys.argv[sys.argv.index("--") + 1:]
src, out_dir = args[0], args[1]
END_CUT = -2.5                 # the end: everything past here (posts, the first planks)
SECTION = (-1.2, 1.0)          # a section of planks and rails from the middle, between two plank gaps
DECK_TOP = 0.2                 # faces centred lower than this are the deck

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
base = next(o for o in bpy.data.objects if o.type == "MESH")
bpy.context.view_layer.objects.active = base
base.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


# the model sags: the section's deck is a little lower at one end; it is levelled (sheared) so sections in a row
# meet. The deck's top near each end of the section:
def deck_top(x):
    return max(v.co.z for v in base.data.vertices if abs(v.co.x - x) < 0.2 and v.co.z < DECK_TOP)


TILT = (deck_top(SECTION[1] - 0.15) - deck_top(SECTION[0] + 0.15)) / (SECTION[1] - SECTION[0] - 0.3)


def piece(name, x0, x1, shift, keep=None, level=False):
    """A copy of the bridge cut to x0..x1, moved by shift along x; keep(face centre) chooses faces, if given."""
    data = base.data.copy()
    o = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(o)
    bm = bmesh.new()
    bm.from_mesh(data)
    for x, side in ((x0, -1), (x1, 1)):
        if x is None:
            continue
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(x, 0, 0), plane_no=(side, 0, 0), clear_outer=True)
    if keep:
        gone = [f for f in bm.faces if not keep(f.calc_center_median())]
        bmesh.ops.delete(bm, geom=gone, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    for v in bm.verts:
        if level:
            v.co.z -= TILT * (v.co.x - x0)
        v.co.x += shift
    bm.to_mesh(data)
    bm.free()
    return o


end = piece("BridgeEnd", None, END_CUT, -END_CUT)
deck = piece("BridgeDeck", SECTION[0], SECTION[1], -SECTION[0], lambda c: c.z < DECK_TOP, level=True)
print(f"levelled the section by {TILT:.3f} m per m")
os.makedirs(out_dir, exist_ok=True)
for o in (end, deck):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    vs = [v.co for v in o.data.vertices]
    lo = [round(min(v[i] for v in vs), 3) for i in range(3)]
    hi = [round(max(v[i] for v in vs), 3) for i in range(3)]
    fbx = os.path.join(out_dir, o.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"MESH"}, bake_anim=False,
                             apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP")
    print(f"wrote {fbx}: {len(o.data.polygons)} faces, from {lo} to {hi}")
