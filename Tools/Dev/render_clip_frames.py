"""Renders 6 frames of each clip in a Meshy animation GLB, the camera following the hips from the side:
    bvenv/bin/python Tools/Dev/render_clip_frames.py -- anim.glb OUT_PREFIX   (writes OUT_PREFIX_0.png .. _5.png)
"""
import bpy, math, os
bpy.ops.wm.read_factory_settings(use_empty=True)
import sys
A = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.import_scene.gltf(filepath=A[0])
sc = bpy.context.scene
arm = [o for o in sc.objects if o.type == 'ARMATURE'][0]
sc.render.engine = 'CYCLES'; sc.cycles.samples = 6; sc.cycles.device = 'CPU'
sc.render.resolution_x, sc.render.resolution_y = 300, 360
sc.render.film_transparent = False
w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.85, 0.9, 0.95, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.2; sc.world = w
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 3.6
cam.location = (5, 0, 1.3); cam.rotation_euler = (math.radians(90), 0, math.radians(90))
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN')); sun.data.energy = 3; sun.rotation_euler = (math.radians(50), 0, math.radians(60)); sc.collection.objects.link(sun)
os.makedirs(os.path.dirname(os.path.abspath(A[1])), exist_ok=True)
for act in bpy.data.actions:
    arm.animation_data.action = act
    a, b = act.frame_range
    for i in range(6):
        f = a + (b - a) * i / 5.0
        sc.frame_set(int(round(f)))
        hb = [b for b in arm.pose.bones if 'hip' in b.name.lower()][0]
        hp = arm.matrix_world @ hb.head
        cam.location = (hp.x + 5, hp.y, hp.z)
        print('POS', a, i, tuple(round(v,2) for v in hp))
        sc.render.filepath = f'{A[1]}_{i}.png'
        bpy.ops.render.render(write_still=True)
    print('done', act.name)
