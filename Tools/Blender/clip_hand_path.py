"""
Where a character's right hand goes in each of its attack clips, to time the game's blows to them: for each clip named
(a part of its name is enough), the hand's height and reach over the clip (at every twentieth of it), and when it is
lowest and fastest. The game plays a clip by the fraction of it (CbModel.Window), so those fractions are the beats.

    python clip_hand_path.py -- Character.fbx Clip_A,Clip_B

(Grak's blows landed early - his hand still going up - until they were set from this.)
"""
import sys, bpy
args = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=args[0])
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
names = set(args[1].split(','))
sc = bpy.context.scene
for act in bpy.data.actions:
    if not any(n in act.name for n in names): continue
    arm.animation_data_create(); arm.animation_data.action = act
    f0, f1 = act.frame_range
    rows = []
    for k in range(41):
        f = f0 + (f1 - f0) * k / 40
        sc.frame_set(int(f), subframe=f - int(f))
        h = arm.matrix_world @ arm.pose.bones['RightHand'].head
        l = arm.matrix_world @ arm.pose.bones['LeftHand'].head
        rows.append((k / 40, h.x, h.y, h.z, l.z))
    zs = [r[3] for r in rows]
    lowk = min(range(len(rows)), key=lambda i: rows[i][3])
    # speed of the right hand between samples
    sp = [((rows[i+1][1]-rows[i][1])**2 + (rows[i+1][2]-rows[i][2])**2 + (rows[i+1][3]-rows[i][3])**2) ** 0.5 for i in range(len(rows)-1)]
    fastk = max(range(len(sp)), key=lambda i: sp[i])
    print(f"{act.name}: frames {f0:.0f}-{f1:.0f}; hand lowest at {rows[lowk][0]:.2f} (z {rows[lowk][3]:.2f}), fastest at {(rows[fastk][0]+rows[fastk+1][0])/2:.2f}")
    print("   z:", " ".join(f"{r[3]:.1f}" for r in rows[::2]))
    print("   y:", " ".join(f"{r[2]:.1f}" for r in rows[::2]))
