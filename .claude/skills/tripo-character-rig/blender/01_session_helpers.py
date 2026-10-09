# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- Live-session status probe (run first; never launch CLI Blender) (Meera call 0) ----
import bpy, os
import codex_live_workflow as live
st = live.status()
result = {"status": st, "pid": os.getpid(), "file": bpy.data.filepath, "is_dirty": bpy.data.is_dirty,
          "scenes": [s.name for s in bpy.data.scenes], "objects": len(bpy.data.objects),
          "version": bpy.app.version_string}

# ---- Fresh unique .blend in the SAME process (token from live.status()) (Meera call 1) ----
import os, uuid
import codex_live_workflow as live
path = "D:/Blender/Companion_Character_Rig_20261008/companion-character-rig-20261008-01.blend"
os.makedirs(os.path.dirname(path), exist_ok=True)
exists = os.path.exists(path)
r = None
if not exists:
    r = live.new_file(path, live.status()["session"], str(uuid.uuid4()))  # read the session token fresh
result = {"exists": exists, "new_file": r}

# ---- Workbench review camera + shoot() helper (renders to <workdir>/review) (Meera call 6) ----
import bpy, math, mathutils, os
scene = bpy.context.scene
ob = [o for o in bpy.data.objects if o.type=='MESH'][0]
# review collection, never exported
rc = bpy.data.collections.get('Review') or bpy.data.collections.new('Review')
if rc.name not in scene.collection.children: scene.collection.children.link(rc)
cam_data = bpy.data.cameras.get('ReviewCam') or bpy.data.cameras.new('ReviewCam')
cam = bpy.data.objects.get('ReviewCam') or bpy.data.objects.new('ReviewCam', cam_data)
if cam.name not in rc.objects: rc.objects.link(cam)
cam_data.type='ORTHO'
scene.camera = cam
scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading
sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'
scene.render.resolution_x = 1000; scene.render.resolution_y = 1200; scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
def shoot(name, view='FRONT', center=(0,0,0.49), ortho=1.05, res=(1000,1200)):
    scene.render.resolution_x, scene.render.resolution_y = res
    c = mathutils.Vector(center)
    d = {'FRONT': (0,-3,0), 'BACK': (0,3,0), 'LEFT': (-3,0,0), 'RIGHT': (3,0,0), 'TOP': (0,0,3)}[view] if isinstance(view,str) else view
    cam.location = c + mathutils.Vector(d)
    direction = c - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z','Y').to_euler()
    cam_data.ortho_scale = ortho
    cam_data.clip_end = 20
    path = f"D:/Blender/Companion_Character_Rig_20261008/review/{name}.png"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path
bpy.app.driver_namespace['companion_shoot'] = shoot
p1 = shoot('src_front','FRONT')
p2 = shoot('src_side','RIGHT', ortho=1.05)
result = {"paths":[p1,p2]}

# ---- submit() wrapper around live.submit (staged visible jobs) (Meera call 9) ----
import bpy, uuid, time
import codex_live_workflow as live
SESSION = live.status()['session']
def submit(title, steps, interval=0.3):
    return live.submit(title=title, expected_session=SESSION, job_id=str(uuid.uuid4()), interval=interval, start_paused=False, steps=steps)
bpy.app.driver_namespace['companion_submit'] = submit
steps = [
 {"label": "Create working copy collection", "code": """
src = bpy.data.objects['tripo_node_17193989-6838-461c-85b8-504bff43c0a6']
src.name = 'Source_Tripo_Mesh'
work = bpy.data.collections.get('Companion_Work') or bpy.data.collections.new('Companion_Work')
if work.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(work)
ob = src.copy(); ob.data = src.data.copy(); ob.name = 'Meera_Body'; ob.data.name = 'Meera_Body'
work.objects.link(ob)
bpy.context.view_layer.layer_collection.children['Source_Tripo'].hide_viewport = True
bpy.data.collections['Source_Tripo'].hide_render = True
"""},
 {"label": "Scale to 1.60 m adult height", "code": """
ob = bpy.data.objects['Meera_Body']
s = 1.60 / 0.979736328125
ob.data.transform(__import__('mathutils').Matrix.Scale(s, 4))
ob.data.update()
"""},
 {"label": "Merge UV-seam split vertices", "code": """
import bmesh
ob = bpy.data.objects['Meera_Body']
bm = bmesh.new(); bm.from_mesh(ob.data)
before = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=2e-6)
after = len(bm.verts)
bm.to_mesh(ob.data); bm.free(); ob.data.update()
bpy.app.driver_namespace['merge_stats'] = (before, after)
"""},
]
r = submit("Prepare working mesh", steps)
result = {"job": r["job"]}

# ---- set_shapes() helper + shape review renders (Meera call 110) ----
import bpy, os
import codex_live_workflow as live
st = live.status()
res = {"job": st["job"]["state"], "err": st["job"]["error"]}
if st["job"]["state"] == 'completed':
    objs = [bpy.data.objects[n] for n in ('Meera_Body','Meera_Mouth','Meera_Eyes')]
    def set_shapes(vals):
        for o in objs:
            if not o.data.shape_keys: continue
            for kb in o.data.shape_keys.key_blocks[1:]:
                kb.value = vals.get(kb.name, 0.0)
        bpy.context.view_layer.update()
    bpy.app.driver_namespace['set_shapes'] = set_shapes
    shoot = bpy.app.driver_namespace['companion_shoot']
    sh = bpy.context.scene.display.shading; sh.color_type='TEXTURE'; sh.light='STUDIO'
    tests = [('neutral', {}), ('V_Open', {'V_Open':1}), ('V_Tight_O', {'V_Tight_O':1}), ('V_Wide', {'V_Wide':1}),
             ('V_Explosive', {'V_Explosive':1}), ('V_Dental_Lip', {'V_Dental_Lip':1}), ('V_Tongue_Out', {'V_Tongue_Out':1}), ('V_Affricate', {'V_Affricate':1}),
             ('blink', {'Eye_Blink_L':1,'Eye_Blink_R':1}), ('smile', {'Mouth_Corner_Pull_L':1,'Mouth_Corner_Pull_R':1}),
             ('frown', {'Mouth_Corner_Depress_L':1,'Mouth_Corner_Depress_R':1,'Brow_Raise_In_L':1,'Brow_Raise_In_R':1}),
             ('wide_eyes_brows', {'Eye_Widen_L':1,'Eye_Widen_R':1,'Brow_Raise_Outer_L':1,'Brow_Raise_Outer_R':1})]
    paths = []
    for name, vals in tests:
        set_shapes(vals)
        paths.append(shoot('shape_'+name, (0.5,-3,0.15), center=(0,-0.05,1.405), ortho=0.15, res=(600,600)))
    set_shapes({})
    res["paths"] = len(paths)
result = res
