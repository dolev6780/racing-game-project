"""
Turns a character rigged and animated by Meshy into one FBX for Unity.

Meshy returns each set of animations as its own GLB, every one carrying the skinned character. This
script keeps the character from the first file, takes the animation clips from all of them, and
removes the clips' travel: the game moves the character itself, so a roll or a jump that also
moved the hips would carry the body away from where the game thinks it is. Hips keep their bob and
crouch, but stay centred over the character's origin and never rise above their rest height.

    blender --background --python Tools/Blender/meshy_character.py -- OUT_DIR NAME anim1.glb [anim2.glb ...]

Writes OUT_DIR/NAME.fbx (mesh, skeleton, every clip) and OUT_DIR/NAME_Albedo.png (base colour).
"""

import os
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
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
    for o in import_glb(path):
        bpy.data.objects.remove(o, do_unlink=True)

clips = [a for a in bpy.data.actions if a.frame_range[1] > a.frame_range[0]]
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
for a in clips:
    a.use_fake_user = True
    curves = []
    for i in range(3):
        try:
            fc = a.fcurves.find(path, index=i)
        except AttributeError:                          # Blender 5: curves live in the action's channelbag
            fc = None
            for layer in a.layers:
                for strip in layer.strips:
                    for bag in strip.channelbags:
                        fc = fc or bag.fcurves.find(path, index=i)
        curves.append(fc)
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
