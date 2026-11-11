"""
Turns a character rigged and animated by Meshy into one FBX for Unity.

Meshy returns each set of animations as its own GLB, every one carrying the skinned character. This
script keeps the character from the first file, takes the animation clips from all of them, and
removes the clips' travel: the game moves the character itself, so a roll or a jump that also
moved the hips would carry the body away from where the game thinks it is. Hips keep their bob and
crouch, but stay centred over the character's origin and never rise above their rest height.

With --upright CLIP[,CLIP...] those clips also lose the hips' rotation: a flip then keeps only its tucked
pose, and the game turns the whole character itself (so it can flip forwards, twice, in its own time).

--drop CLIP[,CLIP...] leaves clips out (ones the game no longer uses).

Clips made with Meshy's text to motion all come named "retarget_clip": a source file named motion_<name>.glb
gives its clip the name Motion_<Name>. --take NEW=CLIP:START:END:YAW then makes a new clip from part of a clip
(START and END as fractions of its length), turned YAW degrees about the vertical: a motion that faces the
wrong way while it does its move is turned to face forward, and only that part kept.

    blender --background --python Tools/Blender/meshy_character.py -- OUT_DIR NAME anim1.glb [anim2.glb ...] [--upright CLIPS] [--drop CLIPS]

Writes OUT_DIR/NAME.fbx (mesh, skeleton, every clip) and OUT_DIR/NAME_Albedo.png (base colour).
"""

import math
import os
import sys

import bpy
from mathutils import Quaternion, Vector

args = sys.argv[sys.argv.index("--") + 1:]
def option(flag):
    if flag not in args:
        return set()
    i = args.index(flag)
    names = set(args[i + 1].split(","))
    del args[i:i + 2]
    return names


upright, drop, takes = option("--upright"), option("--drop"), option("--take")
out_dir, name, sources = args[0], args[1], args[2:]

bpy.ops.wm.read_factory_settings(use_empty=True)


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


# the character, its skeleton and the first set of clips
objs = import_glb(sources[0])
arm = next(o for o in objs if o.type == "ARMATURE")
body = next(o for o in objs if o.type == "MESH" and o.vertex_groups)
for o in objs:
    if o.type == "MESH" and o is not body:   # Meshy leaves a small marker sphere in the file
        bpy.data.objects.remove(o, do_unlink=True)
arm.name, body.name = name, name + "_Body"

# clips from the other files; their copies of the character are thrown away
for path in sources[1:]:
    before = set(bpy.data.actions)
    for o in import_glb(path):
        bpy.data.objects.remove(o, do_unlink=True)
    stem = os.path.splitext(os.path.basename(path))[0]
    if stem.startswith("motion_"):
        for a in set(bpy.data.actions) - before:
            a.name = "Motion_" + stem[len("motion_"):].capitalize()

clips = [a for a in bpy.data.actions if a.frame_range[1] > a.frame_range[0]]
# a file of one action names its clip "Armature|<Clip>|baselayer": named <Clip> like the others
for a in clips:
    parts = a.name.split("|")
    if len(parts) >= 3 and parts[-1] == "baselayer":
        a.name = parts[-2]
for a in list(bpy.data.actions):
    if a not in clips:
        bpy.data.actions.remove(a)

# ---- remove travel from the hips
hips = arm.data.bones[0]
while hips.parent:
    hips = hips.parent
to_arm = hips.matrix_local.to_3x3()                     # hips' rest axes in armature space
up = (arm.matrix_world.to_3x3().inverted() @ Vector((0, 0, 1))).normalized()
path = f'pose.bones["{hips.name}"].location'


def find_curve(a, data_path, index):
    try:
        return a.fcurves.find(data_path, index=index)
    except AttributeError:                              # Blender 5: curves live in the action's channelbag
        for layer in a.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    fc = bag.fcurves.find(data_path, index=index)
                    if fc:
                        return fc
    return None


def hold(curve, value):
    """Replaces a curve's keys with one constant value over the clip."""
    keys = [k.co.x for k in curve.keyframe_points]
    curve.keyframe_points.clear()
    curve.keyframe_points.add(2)
    for k, f in enumerate((min(keys), max(keys))):
        curve.keyframe_points[k].co = (f, value)
    curve.update()


for a in clips:
    a.use_fake_user = True
    if a.name.split("|")[-1] in upright:
        for i, v in enumerate((1.0, 0.0, 0.0, 0.0)):
            c = find_curve(a, f'pose.bones["{hips.name}"].rotation_quaternion', i)
            if c:
                hold(c, v)
        for i in range(3):
            c = find_curve(a, f'pose.bones["{hips.name}"].rotation_euler', i)
            if c:
                hold(c, 0.0)
    curves = [find_curve(a, path, i) for i in range(3)]
    if not all(curves):
        continue
    f0, f1 = (int(round(v)) for v in a.frame_range)
    frames = range(f0, f1 + 1)
    samples = [Vector([c.evaluate(f) for c in curves]) for f in frames]
    fixed = []
    for s in samples:
        p = to_arm @ s                                     # offset from the rest pose, in armature space
        fixed.append(to_arm.inverted() @ (up * min(p.dot(up), 0.0)))   # crouch yes, rise or travel no
    for i, c in enumerate(curves):
        c.keyframe_points.clear()
        c.keyframe_points.add(len(fixed))
        for k, (f, v) in enumerate(zip(frames, fixed)):
            c.keyframe_points[k].co = (f, v[i])
            c.keyframe_points[k].interpolation = "LINEAR"
        c.update()


# ---- clips taken from part of another, turned to face forward
def all_curves(a):
    try:
        return list(a.fcurves)
    except AttributeError:
        return [c for layer in a.layers for strip in layer.strips for bag in strip.channelbags for c in bag.fcurves]


arm_rest = hips.matrix_local.to_quaternion()
for spec in takes:
    new, rest = spec.split("=")
    src, start, end, yaw = rest.split(":")
    a = next(x for x in clips if x.name.split("|")[-1] == src).copy()
    a.name = new
    a.use_fake_user = True
    f0, f1 = a.frame_range
    lo, hi = f0 + float(start) * (f1 - f0), f0 + float(end) * (f1 - f0)
    turn = Quaternion(up, math.radians(float(yaw)))
    # the hips' rotation is relative to their rest pose: turned in armature space, it becomes rest^-1 * turn * rest * q
    qc = [find_curve(a, f'pose.bones["{hips.name}"].rotation_quaternion', i) for i in range(4)]
    if all(qc):
        keys = sorted({k.co.x for c in qc for k in c.keyframe_points})
        values = []
        for f in keys:
            q = Quaternion([c.evaluate(f) for c in qc])
            values.append(arm_rest.inverted() @ turn @ arm_rest @ q)
        for i, c in enumerate(qc):
            c.keyframe_points.clear()
            c.keyframe_points.add(len(keys))
            for k, (f, q) in enumerate(zip(keys, values)):
                c.keyframe_points[k].co = (f, q[i])
                c.keyframe_points[k].interpolation = "LINEAR"
    # keep only lo..hi, moved to start at frame 0
    for c in all_curves(a):
        inside = [(k.co.x, k.co.y) for k in c.keyframe_points if lo - 1e-3 <= k.co.x <= hi + 1e-3]
        if not inside:
            inside = [(lo, c.evaluate(lo)), (hi, c.evaluate(hi))]
        c.keyframe_points.clear()
        c.keyframe_points.add(len(inside))
        for k, (f, v) in enumerate(inside):
            c.keyframe_points[k].co = (f - lo, v)
            c.keyframe_points[k].interpolation = "LINEAR"
        c.update()
    a.use_frame_range = True
    a.frame_start, a.frame_end = 0, hi - lo
    clips.append(a)
    print(f"took {new} from {src} {start}..{end}, turned {yaw} degrees")

# ---- base colour as a plain PNG next to the FBX (the game builds its own material)
os.makedirs(out_dir, exist_ok=True)
for m in body.data.materials:
    for n in (m.node_tree.nodes if m and m.node_tree else []):
        if n.type == "BSDF_PRINCIPLED" and n.inputs["Base Color"].is_linked:
            img = n.inputs["Base Color"].links[0].from_node.image
            img.filepath_raw = os.path.join(out_dir, name + "_Albedo.png")
            img.file_format = "PNG"
            img.save()

# ---- one FBX with every clip
ad = arm.animation_data or arm.animation_data_create()
for track in list(ad.nla_tracks):
    ad.nla_tracks.remove(track)
ad.action = None
clips = [a for a in clips if a.name.split("|")[-1] not in drop]
for track_action in clips:                              # an NLA strip per clip: the exporter writes each as a take
    tr = ad.nla_tracks.new()
    tr.name = track_action.name
    tr.strips.new(track_action.name, int(track_action.frame_range[0]), track_action)
for o in bpy.context.scene.objects:
    o.select_set(o in (arm, body))
bpy.context.view_layer.objects.active = arm
fbx = os.path.join(out_dir, name + ".fbx")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
                         bake_anim_force_startend_keying=True, apply_scale_options="FBX_SCALE_ALL",
                         mesh_smooth_type="FACE", path_mode="STRIP")
print(f"wrote {fbx} with {len(clips)} clips: {', '.join(sorted(a.name for a in clips))}")
