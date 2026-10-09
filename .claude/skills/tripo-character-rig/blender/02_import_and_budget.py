# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- Import GLB into Source_Tripo collection (Meera call 3) ----
import uuid
import codex_live_workflow as live
steps = [
 {"label": "Import Tripo GLB source", "code": """
bpy.ops.import_scene.gltf(filepath=r'C:/Users/user/Downloads/3d character model.glb')
coll = bpy.data.collections.new('Source_Tripo')
bpy.context.scene.collection.children.link(coll)
for o in list(bpy.context.selected_objects):
    for c in list(o.users_collection): c.objects.unlink(o)
    coll.objects.link(o)
"""},
]
r = live.submit(title="Import companion character source", expected_session=live.status()["session"],
                job_id=str(uuid.uuid4()), interval=0.5, start_paused=False, steps=steps)
result = r

# ---- Inspect objects (Meera call 4) ----
import bpy
import codex_live_workflow as live
st = live.status()
info = {}
for o in bpy.data.objects:
    d = {"type": o.type, "loc": list(o.location), "rot": list(o.rotation_euler), "scale": list(o.scale), "dims": list(o.dimensions)}
    if o.type == 'MESH':
        me = o.data
        d.update({"verts": len(me.vertices), "polys": len(me.polygons), "uv": [u.name for u in me.uv_layers], "mats": [m.name for m in me.materials if m]})
    info[o.name] = d
result = {"job": st["job"], "objects": info}

# ---- Connected components of the fused mesh (Meera call 12) ----
import bpy, numpy as np, time
t0=time.time()
ob = bpy.data.objects['Meera_Body']; me = ob.data
n = len(me.vertices)
co = np.empty(n*3, np.float32); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
ed = np.empty(len(me.edges)*2, np.int32); me.edges.foreach_get('vertices', ed); ed = ed.reshape(-1,2)
u, v = ed[:,0], ed[:,1]
parent = np.arange(n, dtype=np.int64)
for it in range(200):
    pu, pv = parent[u], parent[v]
    lo = np.minimum(pu, pv); hi = np.maximum(pu, pv)
    changed = (lo != hi)
    if not changed.any(): break
    np.minimum.at(parent, hi[changed], lo[changed])
    while True:
        pp = parent[parent]
        if (pp == parent).all(): break
        parent = pp
labels = parent
uniq, inv, counts = np.unique(labels, return_inverse=True, return_counts=True)
order = np.argsort(-counts)
comps = []
for k in order[:40]:
    m = inv == k
    c = co[m]
    comps.append({"n": int(counts[k]), "min": [round(float(x),3) for x in c.min(0)], "max": [round(float(x),3) for x in c.max(0)]})
bpy.app.driver_namespace['comp_labels'] = inv
result = {"iters": it, "ncomp": int(len(uniq)), "secs": round(time.time()-t0,2), "top": comps, "small_total": int(counts[order[40:]].sum()) if len(order)>40 else 0}

# ---- Region face counts (Meera call 14) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
nf = len(me.polygons)
fc = np.empty(nf*3, np.float32); me.polygons.foreach_get('center', fc); fc = fc.reshape(-1,3)
fa = np.empty(nf, np.float32); me.polygons.foreach_get('area', fa)
def cnt(mask): return int(mask.sum()), round(float(fa[mask].sum()),4)
head = (fc[:,2] > 1.30)
face_front = head & (fc[:,1] < -0.05) & (np.abs(fc[:,0]) < 0.11) & (fc[:,2] < 1.52)
hands = np.abs(fc[:,0]) > 0.50
feet = fc[:,2] < 0.10
result = {"faces": nf, "total_area": round(float(fa.sum()),3), "head": cnt(head), "face_front": cnt(face_front), "hands": cnt(hands), "feet": cnt(feet),
          "median_edge_mm": round(float(np.sqrt(np.median(fa)*2)*1000),3)}

# ---- Per-region decimation to ~80k tris (face/hands kept denser) (Meera call 15) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
common = """
import numpy as np
def region_select(kind):
    ob = bpy.data.objects['Meera_Body']; me = ob.data
    if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    nf = len(me.polygons)
    fc = np.empty(nf*3, np.float32); me.polygons.foreach_get('center', fc); fc = fc.reshape(-1,3)
    x,y,z = fc[:,0], fc[:,1], fc[:,2]
    face = (z>1.33)&(z<1.52)&(y<-0.04)&(np.abs(x)<0.115)
    headrest = (z>1.28)&~face
    hands = (np.abs(x)>0.50)&~(z>1.28)
    feet = (z<0.10)
    body = ~(face|headrest|hands|feet)
    sel = {'face':face,'headrest':headrest,'hands':hands,'feet':feet,'body':body}[kind]
    me.polygons.foreach_set('select', sel.astype(bool))
    me.vertices.foreach_set('select', np.zeros(len(me.vertices), bool))
    me.edges.foreach_set('select', np.zeros(len(me.edges), bool))
    me.update()
    return int(sel.sum())
def decimate_region(kind, ratio):
    ob = bpy.data.objects['Meera_Body']
    for o in bpy.context.view_layer.objects: o.select_set(False)
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    n = region_select(kind)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.mesh.decimate(ratio=ratio)
    bpy.ops.object.mode_set(mode='OBJECT')
    stats = bpy.app.driver_namespace.setdefault('decimate_stats', [])
    stats.append((kind, n, len(ob.data.polygons)))
"""
steps = [
 {"label": "Decimation helpers", "code": common},
 {"label": "Decimate shoes (dense laces)", "code": "decimate_region('feet', 0.015)"},
 {"label": "Decimate clothing and limbs", "code": "decimate_region('body', 0.03)"},
 {"label": "Decimate hands (finger detail kept)", "code": "decimate_region('hands', 0.08)"},
 {"label": "Decimate hair, ears and earrings", "code": "decimate_region('headrest', 0.06)"},
 {"label": "Decimate face (expression detail kept)", "code": "decimate_region('face', 0.22)"},
]
r = submit("Real-time mesh budget", steps)
result = {"job": r["job"]["state"]}

# ---- Clear stale custom normals, smooth shade, review (Meera call 18) ----
import bpy
ob = bpy.data.objects['Meera_Body']
# clear stale custom normals from the import, smooth shade
me = ob.data
try:
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob], selected_editable_objects=[ob]):
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
except Exception as e:
    err = str(e)
me.polygons.foreach_set('use_smooth', [True]*len(me.polygons)); me.update()
shoot = bpy.app.driver_namespace['companion_shoot']
s = 1.6/0.979736328125
p0 = shoot('dec_front','FRONT', center=(0,0,0.8), ortho=1.75, res=(900,1100))
p1 = shoot('dec_face','FRONT', center=(0,0,1.404), ortho=0.2613, res=(1000,1000))
bpy.context.scene.display.shading.color_type = 'SINGLE'
bpy.context.scene.display.shading.show_xray = False
sh = bpy.context.scene.display.shading
p2 = shoot('dec_face_geo','FRONT', center=(0,0,1.404), ortho=0.2613, res=(1000,1000))
bpy.context.scene.display.shading.color_type = 'TEXTURE'
result = {"tris": len(me.polygons), "verts": len(me.vertices)}
