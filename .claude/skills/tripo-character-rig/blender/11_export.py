# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- Save textures raw (NOT save_render: AgX darkens) (Meera call 132) ----
import bpy, numpy as np, os
tex = 'D:/Blender/Companion_Character_Rig_20261008/export/textures'
out = {}
def save_raw(name, path, fmt, quality=None):
    img = bpy.data.images[name]
    img.filepath_raw = path; img.file_format = fmt
    if quality is not None: img.save(filepath=path, quality=quality)
    else: img.save(filepath=path)
    out[name] = os.path.getsize(path)
save_raw('Meera_BaseColor', tex + '/Meera_BaseColor.jpg', 'JPEG', 92)
nm = bpy.data.images.get('Meera_Normal')
save_raw('Meera_Normal', tex + '/Meera_Normal.png', 'PNG')
save_raw('Meera_MetallicSmoothness', tex + '/Meera_MetallicSmoothness.png', 'PNG')
save_raw('Meera_Eyes', tex + '/Meera_Eyes.png', 'PNG')
save_raw('Meera_Mouth', tex + '/Meera_Mouth.png', 'PNG')
# verify normal map pixels vs source
src = bpy.data.images['3d+character+model_normal.png']; W,H = src.size
spx = np.empty(W*H*4, np.float32); src.pixels.foreach_get(spx); spx = spx.reshape(H,W,4)
result = {"sizes": out, "normal_src_sample": [round(float(v),3) for v in spx[1000,1000,:3]], "bc_colorspace": bpy.data.images['Meera_BaseColor'].colorspace_settings.name, "nm_colorspace": nm.colorspace_settings.name}

# ---- Spring chains + colliders for Unity (Meera call 133) ----
import bpy, json, numpy as np
rig = bpy.data.objects['Meera_Rig']
B = {b.name: b for b in rig.data.bones}
bones = {n: list(map(float, b.head_local)) for n, b in B.items() if b.use_deform}
def chain(prefix, names, stiff, drag, grav, rad):
    return {"name": prefix, "bones": names, "tip": list(map(float, B[names[-1]].tail_local)), "stiffness": stiff, "drag": drag, "gravity": grav, "radius": rad}
chains = []
hair_groups = {}
for n in B:
    if n.startswith('Hair_'): hair_groups.setdefault(n.rsplit('_',1)[0], []).append(n)
for g, ns in sorted(hair_groups.items()):
    chains.append(chain(g, sorted(ns), 0.55, 0.35, 0.25, 0.018))
for s in ('L','R'):
    chains.append(chain(f'Earring_{s}', [f'Earring_{s}_01', f'Earring_{s}_02'], 0.22, 0.12, 0.65, 0.007))
for k in ('F','FL','FR','B','BL','BR'):
    chains.append(chain(f'Kurti_{k}', [f'Kurti_{k}_01', f'Kurti_{k}_02'], 0.60, 0.40, 0.15, 0.022))
for s in ('L','R'):
    chains.append(chain(f'Sleeve_{s}', [f'Sleeve_{s}_01', f'Sleeve_{s}_02'], 0.20, 0.30, 0.80, 0.018))
cols = [
 {"bone": "CC_Base_Head", "center": [0,-0.03,1.47], "radius": 0.085},
 {"bone": "CC_Base_Head", "center": [0,-0.09,1.42], "radius": 0.055},
 {"bone": "CC_Base_NeckTwist01", "center": [0,-0.012,1.27], "tail": [0,-0.012,1.36], "radius": 0.040},
 {"bone": "CC_Base_Spine02", "center": [0,-0.01,1.14], "radius": 0.105},
 {"bone": "CC_Base_Spine02", "center": [0,0.02,1.20], "radius": 0.095},
 {"bone": "CC_Base_Spine01", "center": [0,0.0,1.03], "radius": 0.10},
 {"bone": "CC_Base_Waist", "center": [0,0.0,0.90], "radius": 0.11},
 {"bone": "CC_Base_Hip", "center": [0,0.0,0.79], "radius": 0.115},
]
for s, sg in (('L',1),('R',-1)):
    cols.append({"bone": f"CC_Base_{s}_Upperarm", "center": [sg*0.16,0.004,1.238], "tail": [sg*0.335,0.012,1.228], "radius": 0.040})
    cols.append({"bone": f"CC_Base_{s}_Thigh", "center": [sg*0.082,-0.002,0.645], "tail": [sg*0.089,-0.008,0.36], "radius": 0.070})
# rest-pose violations: joints (bone heads + tip) inside colliders
def dist_to(c, p):
    a = np.array(c['center']); r = c['radius']
    if 'tail' in c:
        b = np.array(c['tail']); d = b - a; t = np.clip(((p - a) @ d)/(d @ d), 0, 1); return np.linalg.norm(p - (a + t*d)) - r
    return np.linalg.norm(p - a) - r
viol = []
for ch in chains:
    pts = [np.array(B[n].head_local) for n in ch['bones'][1:]] + [np.array(ch['tip'])]
    for i, p in enumerate(pts):
        for ci, c in enumerate(cols):
            d = dist_to(c, p) - ch['radius']
            if d < 0: viol.append((ch['name'], i, ci, c['bone'], round(float(d)*1000,1)))
data = {"source": "D:/Blender/Companion_Character_Rig_20261008/companion-character-rig-20261008-01.blend", "space": "blender_z_up_meters", "bones": bones, "chains": chains, "colliders": cols}
bpy.app.driver_namespace['rig_json'] = data
result = {"chains": len(chains), "colliders": len(cols), "violations": viol}

# ---- Shrink colliders off rest joints (Meera call 134) ----
import bpy, json, numpy as np
data = bpy.app.driver_namespace['rig_json']
rig = bpy.data.objects['Meera_Rig']; B = {b.name: b for b in rig.data.bones}
joints = []
for ch in data['chains']:
    for n in ch['bones'][1:]: joints.append((np.array(B[n].head_local), ch['radius']))
    joints.append((np.array(ch['tip']), ch['radius']))
def seg_dist(c, p):
    a = np.array(c['center'])
    if 'tail' in c:
        b = np.array(c['tail']); d = b - a; t = np.clip(((p - a) @ d)/(d @ d), 0, 1); return np.linalg.norm(p - (a + t*d))
    return np.linalg.norm(p - a)
fit = []
for c in data['colliders']:
    allowed = min(seg_dist(c, p) - jr - 0.002 for p, jr in joints)
    new = float(min(c['radius'], allowed))
    fit.append((c['bone'], round(c['radius'],3), round(new,3)))
    c['radius'] = new
data['colliders'] = [c for c in data['colliders'] if c['radius'] >= 0.025]
import os
path = 'D:/Blender/Companion_Character_Rig_20261008/export/Meera.rig.json'
with open(path, 'w') as fh: json.dump(data, fh, indent=1)
result = {"fit": fit, "kept": len(data['colliders']), "path": path}

# ---- Write <Name>.rig.json (Meera call 135) ----
import bpy, json
data = bpy.app.driver_namespace['rig_json']
out = {
  "source": data["source"],
  "space": "blender_z_up_meters",
  "bones": [{"name": n, "head": v} for n, v in sorted(data["bones"].items())],
  "chains": data["chains"],
  "colliders": [{"bone": c["bone"], "center": c["center"], "tail": c.get("tail", c["center"]), "capsule": "tail" in c, "radius": c["radius"]} for c in data["colliders"]],
}
path = 'D:/Blender/Companion_Character_Rig_20261008/export/Meera.rig.json'
with open(path, 'w') as fh: json.dump(out, fh, indent=1)
result = {"bones": len(out["bones"]), "chains": len(out["chains"]), "colliders": len(out["colliders"])}

# ---- FINAL code_fbx (rest pose, deform-only, -Z forward, Y up, FBX_SCALE_ALL) ----
# Stage body for live.submit; stored in bpy.app.driver_namespace under the same name.
code_fbx = r'''

out = 'D:/Blender/Companion_Character_Rig_20261008/export'
rig = bpy.data.objects['Meera_Rig']
objs = [rig] + [bpy.data.objects[n] for n in ('Meera_Body', 'Meera_Eyes', 'Meera_Mouth')]
for o in objs:
    if o.data and getattr(o.data, 'shape_keys', None):
        for kb in o.data.shape_keys.key_blocks: kb.value = 0.0
rig.data.pose_position = 'REST'
bpy.context.view_layer.update()
for o in bpy.context.view_layer.objects: o.select_set(False)
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=out + '/Meera.fbx', use_selection=True, object_types={'ARMATURE', 'MESH'},
    use_mesh_modifiers=False, mesh_smooth_type='OFF', use_tspace=False, colors_type='NONE',
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=True,
    armature_nodetype='NULL', bake_anim=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=False, path_mode='STRIP', embed_textures=False)
rig.data.pose_position = 'POSE'
bpy.ops.wm.save_mainfile()
'''
