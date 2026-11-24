import sys, bpy, glob
a = sys.argv[sys.argv.index("--") + 1:]
for f in sorted(glob.glob(a[0] + "/**/*.fbx", recursive=True)):
    if f.endswith("_Clips.fbx") or "_LOD1" in f: continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try: bpy.ops.import_scene.fbx(filepath=f)
    except Exception as e: print("ERR", f); continue
    n = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
    print(n, f.split("Resources/Cloudborne/")[1])
