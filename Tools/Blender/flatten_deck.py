"""
Flattens a humped walkway along a long prop (the long stone bridge arched up in the middle, while the game has Raja
walk it level: the user saw a crooked bridge). The top of the model is measured in slices along X; a smooth
curve (a parabola) is fitted through those tops, and every vertex is moved down by how far that curve rises above
its lowest point at its X, so the whole model, arches and all, is straightened and its top runs level. Then it is
set back on Z = 0 and exported as the props are (Tools/Blender/meshy_prop.py). Prints the walkway's height over the
model's foot and the model's height, for the game's constants.

    python flatten_deck.py -- IN.fbx OUT.fbx
"""

import sys

import bpy
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
obj = next(o for o in bpy.data.objects if o.type == "MESH")
mesh = obj.data
pts = np.array([v.co[:] for v in mesh.vertices])
x, z = pts[:, 0], pts[:, 2]

# the top in slices along X (the middle 90%: its ends fall away), and a parabola through them
lo, hi = x.min(), x.max()
xs, tops = [], []
for c in np.linspace(lo + 0.05 * (hi - lo), hi - 0.05 * (hi - lo), 24):
    sel = np.abs(x - c) < (hi - lo) / 48
    if sel.any():
        xs.append(c)
        tops.append(z[sel].max())
curve = np.poly1d(np.polyfit(xs, tops, 2))
rise = curve(x) - curve(np.array(xs)).min()
z = z - rise
z = z - z.min()
for v, nz in zip(mesh.vertices, z):
    v.co.z = nz
mesh.update()

deck = np.median([z[np.abs(x - c) < (hi - lo) / 48].max() for c in xs])
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"}, bake_anim=False,
                         apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP")
print(f"wrote {out}: walkway {deck:.3f} over its foot, height {z.max():.3f}, hump removed {rise.max():.3f}")
