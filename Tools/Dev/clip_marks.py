# For each clip in the GLBs: per 10% of its length, the hips' height and the hands' reach forward (front = -Y) and up.
import sys, bpy
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
for f in a:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=f)
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    sc = bpy.context.scene
    act = arm.animation_data.action
    a0, a1 = act.frame_range
    pb = arm.pose.bones
    def P(n): return arm.matrix_world @ pb[n].head
    print("==", f.split("/")[-1], act.name, int(a1 - a0), "frames")
    for i in range(11):
        sc.frame_set(int(round(a0 + (a1 - a0) * i / 10)))
        h = P("Hips"); lh = P("LeftHand"); rh = P("RightHand"); lf = P("LeftFoot"); rf = P("RightFoot")
        fwd = max(-(lh.y - h.y), -(rh.y - h.y)); up = max(lh.z, rh.z) - h.z
        print(f"{i/10:.1f} hips z {h.z:.2f} y {h.y:.2f} | hand fwd {fwd:.2f} up {up:.2f} | feet z {min(lf.z, rf.z):.2f} fwd {max(-(lf.y-h.y), -(rf.y-h.y)):.2f}")
