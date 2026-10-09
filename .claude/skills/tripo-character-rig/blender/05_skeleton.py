# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- Measure fingers/thumb from geometry (Meera call 42) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
nv = len(me.vertices); nf = len(me.polygons)
co = np.empty(nv*3, np.float32); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
lab = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', lab)
fv = np.empty(nf*3, np.int32); me.polygons.foreach_get('vertices', fv); fv = fv.reshape(-1,3)
ed = np.empty(len(me.edges)*2, np.int32); me.edges.foreach_get('vertices', ed); ed = ed.reshape(-1,2)
vskin = np.zeros(nv, bool); vskin[fv[lab==0].ravel()] = True
def components(mask):
    e = ed[mask[ed[:,0]] & mask[ed[:,1]]]
    parent = np.arange(nv)
    for _ in range(100):
        pu, pv = parent[e[:,0]], parent[e[:,1]]
        lo = np.minimum(pu,pv); hi = np.maximum(pu,pv); ch = lo != hi
        if not ch.any(): break
        np.minimum.at(parent, hi[ch], lo[ch])
        while True:
            pp = parent[parent]
            if (pp == parent).all(): break
            parent = pp
    ids = np.unique(parent[mask])
    return [np.nonzero(mask & (parent == i))[0] for i in ids]
out = {}
for side, sg in (('L',1),('R',-1)):
    m = vskin & (sg*co[:,0] > 0.618)
    comps = sorted(components(m), key=lambda c: co[c,1].mean())
    fingers = []
    for c in comps:
        if len(c) < 15: continue
        p = co[c]
        fingers.append({"n": len(c), "y_mean": round(float(p[:,1].mean()),4), "z_mean": round(float(p[:,2].mean()),4), "tip_x": round(float((sg*p[:,0]).max()),4)})
    # thumb: skin verts in front of the index finger, below palm
    tm = vskin & (sg*co[:,0] > 0.525) & (sg*co[:,0] < 0.625) & (co[:,1] < -0.05)
    tcomps = sorted(components(tm), key=lambda c: -len(c))
    th = co[tcomps[0]] if tcomps else None
    out[side] = {"fingers": fingers, "thumb": None if th is None else {"n": len(th), "min": [round(float(v),4) for v in th.min(0)], "max": [round(float(v),4) for v in th.max(0)]}}
result = out

# ---- Hair strand sectors (to place chains) (Meera call 46) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
nv = len(me.vertices); nf = len(me.polygons)
co = np.empty(nv*3, np.float32); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
lab = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', lab)
fv = np.empty(nf*3, np.int32); me.polygons.foreach_get('vertices', fv); fv = fv.reshape(-1,3)
vh = np.zeros(nv, bool); vh[fv[lab==1].ravel()] = True
H = co[vh]
# angle sectors around a vertical axis through (0, 0.0)
ang = np.degrees(np.arctan2(H[:,0], H[:,1]))   # 0 = back (+y), 90 = left (+x), -90 = right, 180 = front
sectors = {"back": (-22.5, 22.5), "back_L": (22.5, 67.5), "L": (67.5, 112.5), "front_L": (112.5, 157.5), "back_R": (-67.5, -22.5), "R": (-112.5, -67.5), "front_R": (-157.5, -112.5)}
out = {}
for name, (a0, a1) in sectors.items():
    m = (ang >= a0) & (ang < a1)
    P = H[m]
    rows = []
    for z in np.arange(1.55, 0.65, -0.05):
        mm = (P[:,2] < z) & (P[:,2] >= z-0.05)
        if mm.sum() < 5: continue
        Q = P[mm]
        rows.append([round(float(z-0.025),3), int(mm.sum()), round(float(Q[:,0].mean()),3), round(float(Q[:,1].mean()),3)])
    out[name] = {"n": int(m.sum()), "zmin": round(float(P[:,2].min()),3) if len(P) else None, "rows": rows}
result = out

# ---- Body cross-sections for joint placement (Meera call 48) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
nv = len(me.vertices); nf = len(me.polygons)
co = np.empty(nv*3, np.float32); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
lab = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', lab)
fv = np.empty(nf*3, np.int32); me.polygons.foreach_get('vertices', fv); fv = fv.reshape(-1,3)
def vmask(cls):
    m = np.zeros(nv, bool); m[fv[np.isin(lab, cls)].ravel()] = True; return m
skin = vmask([0]); kurti = vmask([2]); pants = vmask([3])
def sect(mask, zlo, zhi, xlo=-1, xhi=1):
    m = mask & (co[:,2]>=zlo) & (co[:,2]<zhi) & (co[:,0]>=xlo) & (co[:,0]<xhi)
    P = co[m]
    if len(P)==0: return None
    return {"n": int(m.sum()), "min": [round(float(v),3) for v in P.min(0)], "max": [round(float(v),3) for v in P.max(0)], "mean": [round(float(v),3) for v in P.mean(0)]}
out = {
 "neck_1.30": sect(skin, 1.295, 1.305, -0.07, 0.07),
 "neck_1.28": sect(skin, 1.275, 1.285, -0.07, 0.07),
 "neck_1.33": sect(skin, 1.325, 1.335, -0.07, 0.07),
 "chest_1.10_kurti": sect(kurti, 1.095, 1.105, -0.25, 0.25),
 "waist_0.95": sect(kurti, 0.945, 0.955, -0.25, 0.25),
 "hips_0.80": sect(kurti, 0.795, 0.805, -0.3, 0.3),
 "pants_0.62": sect(pants, 0.615, 0.625),
 "arm_x0.25": sect(kurti, 1.0, 1.4, 0.245, 0.255),
 "arm_x0.33": sect(kurti, 1.0, 1.4, 0.325, 0.335),
 "arm_x0.20": sect(kurti, 1.0, 1.4, 0.195, 0.205),
 "wrist_x0.495": sect(skin, 1.0, 1.4, 0.49, 0.50),
 "wrist_x0.52": sect(skin, 1.0, 1.4, 0.515, 0.525),
 "knuckle_x0.585": sect(skin, 1.0, 1.4, 0.58, 0.59),
 "ankle_z0.08": sect(pants, 0.075, 0.085, 0.0, 0.3),
 "shoe_top": sect(vmask([4]), 0.0, 0.2, 0.0, 0.3),
 "head_skin_all": sect(skin, 1.30, 1.7, -0.2, 0.2),
}
result = out

# ---- Landmarks + CC_Base skeleton + spring chains (hair, earrings, kurti, sleeves) (Meera call 49) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
landmark_code = r'''
import numpy as np, mathutils
V = mathutils.Vector
ob = bpy.data.objects['Meera_Body']; me = ob.data
nv = len(me.vertices); nf = len(me.polygons)
co = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
lab = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', lab)
fv = np.empty(nf*3, np.int32); me.polygons.foreach_get('vertices', fv); fv = fv.reshape(-1,3)
def vmask(cls):
    m = np.zeros(nv, bool); m[fv[np.isin(lab, cls)].ravel()] = True; return m
skin = vmask([0]); kurti = vmask([2])
B = {}   # name -> (head, tail, parent, deform)
def add(name, head, tail, parent, deform=True): B[name] = (V(head), V(tail), parent, deform)
add('CC_Base_BoneRoot', (0,0,0), (0,0,0.10), None)
add('CC_Base_Hip', (0,0.0,0.665), (0,0.0,0.75), 'CC_Base_BoneRoot')
add('CC_Base_Pelvis', (0,0.0,0.665), (0,0.0,0.595), 'CC_Base_Hip')
add('CC_Base_Waist', (0,0.0,0.75), (0,0.005,0.87), 'CC_Base_Hip')
add('CC_Base_Spine01', (0,0.005,0.87), (0,0.005,1.0), 'CC_Base_Waist')
add('CC_Base_Spine02', (0,0.005,1.0), (0,0.0,1.235), 'CC_Base_Spine01')
add('CC_Base_NeckTwist01', (0,-0.002,1.255), (0,-0.004,1.31), 'CC_Base_Spine02')
add('CC_Base_NeckTwist02', (0,-0.004,1.31), (0,-0.004,1.36), 'CC_Base_NeckTwist01')
add('CC_Base_Head', (0,-0.004,1.36), (0,-0.004,1.55), 'CC_Base_NeckTwist02')
add('CC_Base_JawRoot', (0,-0.032,1.405), (0,-0.128,1.338), 'CC_Base_Head')
for side, sg in (('L',1),('R',-1)):
    c, r = bpy.app.driver_namespace['eye_fit_'+side]
    add(f'CC_Base_{side}_Eye', tuple(c), (c[0], c[1]-r, c[2]), 'CC_Base_Head')
    # legs
    add(f'CC_Base_{side}_Thigh', (sg*0.082,-0.002,0.645), (sg*0.089,-0.008,0.36), 'CC_Base_Pelvis')
    add(f'CC_Base_{side}_Calf', (sg*0.089,-0.008,0.36), (sg*0.093,0.035,0.078), f'CC_Base_{side}_Thigh')
    add(f'CC_Base_{side}_Foot', (sg*0.093,0.035,0.078), (sg*0.094,-0.085,0.022), f'CC_Base_{side}_Calf')
    add(f'CC_Base_{side}_ToeBase', (sg*0.094,-0.085,0.022), (sg*0.095,-0.158,0.02), f'CC_Base_{side}_Foot')
    # arms
    add(f'CC_Base_{side}_Clavicle', (sg*0.018,-0.018,1.245), (sg*0.16,0.004,1.238), 'CC_Base_Spine02')
    add(f'CC_Base_{side}_Upperarm', (sg*0.16,0.004,1.238), (sg*0.335,0.012,1.228), f'CC_Base_{side}_Clavicle')
    add(f'CC_Base_{side}_Forearm', (sg*0.335,0.012,1.228), (sg*0.497,-0.008,1.222), f'CC_Base_{side}_Upperarm')
    # fingers from the actual geometry
    hand = skin & (sg*co[:,0] > 0.49)
    H = co[hand]; hx = sg*H[:,0]
    centers = {'Index': -0.0388, 'Mid': -0.0168, 'Ring': 0.006, 'Pinky': 0.0264}
    mcpx = {'Index': 0.583, 'Mid': 0.585, 'Ring': 0.582, 'Pinky': 0.577}
    tips = {}
    for fname, yk in centers.items():
        m = (np.abs(H[:,1]-yk) < 0.0095) & (hx > 0.605)
        P = H[m]; px = sg*P[:,0]
        tipx = float(px.max()) - 0.003
        # centerline: fit y(x), z(x) on bins
        bins = np.arange(0.61, tipx, 0.004)
        cx, cy, cz = [], [], []
        for b0 in bins:
            mm = (px >= b0) & (px < b0+0.004)
            if mm.sum() < 3: continue
            cx.append(b0+0.002); cy.append(P[mm,1].mean()); cz.append((P[mm,2].min()+P[mm,2].max())/2)
        cx = np.array(cx); ay = np.polyfit(cx, cy, 1); az = np.polyfit(cx, cz, 1)
        def at(xv): return V((sg*xv, float(np.polyval(ay, xv)), float(np.polyval(az, xv))))
        mx = mcpx[fname]
        mcp = at(mx); mcp.z += 0.002
        tip = at(tipx)
        pip = mcp.lerp(tip, 0.45); dip = mcp.lerp(tip, 0.75)
        tips[fname] = (mcp, pip, dip, tip)
        add(f'CC_Base_{side}_{fname}1', mcp, pip, f'CC_Base_{side}_Hand')
        add(f'CC_Base_{side}_{fname}2', pip, dip, f'CC_Base_{side}_{fname}1')
        add(f'CC_Base_{side}_{fname}3', dip, tip, f'CC_Base_{side}_{fname}2')
    add(f'CC_Base_{side}_Hand', (sg*0.497,-0.008,1.222), tuple(tips['Mid'][0]), f'CC_Base_{side}_Forearm')
    # thumb
    tm = skin & (sg*co[:,0] > 0.527) & (sg*co[:,0] < 0.625) & (co[:,1] < -0.05) & (co[:,2] < 1.222)
    T = co[tm]
    cmc = V((sg*0.512, -0.030, 1.206))
    d = np.linalg.norm(T - np.array(cmc), axis=1)
    far = T[np.argsort(-d)[:12]].mean(0)
    tip = V(tuple(far)); tip = cmc + (tip - cmc) * 0.985
    mcp = cmc.lerp(tip, 0.40); ip = cmc.lerp(tip, 0.70)
    add(f'CC_Base_{side}_Thumb1', cmc, mcp, f'CC_Base_{side}_Hand')
    add(f'CC_Base_{side}_Thumb2', mcp, ip, f'CC_Base_{side}_Thumb1')
    add(f'CC_Base_{side}_Thumb3', ip, tip, f'CC_Base_{side}_Thumb2')
# hair chains (parent head)
hair = {
 'Back_C': [(0,0.11,1.45),(0,0.15,1.30),(0,0.155,1.15),(0,0.11,1.02),(0,0.10,0.91)],
 'Back_L': [(0.09,0.09,1.43),(0.12,0.11,1.28),(0.138,0.13,1.16),(0.108,0.074,1.03),(0.085,0.08,0.925)],
 'Back_R': [(-0.09,0.09,1.43),(-0.118,0.108,1.28),(-0.124,0.11,1.16),(-0.075,0.07,1.03),(-0.075,0.085,0.913)],
 'Side_L': [(0.122,0.0,1.40),(0.12,0.02,1.28),(0.165,0.058,1.19),(0.118,0.04,1.08),(0.08,0.027,0.97)],
 'Side_R': [(-0.13,0.0,1.40),(-0.135,-0.005,1.30),(-0.175,-0.01,1.22),(-0.12,0.02,1.10),(-0.09,0.03,0.976)],
 'Front_R': [(-0.095,-0.105,1.43),(-0.106,-0.09,1.33),(-0.162,-0.122,1.21),(-0.14,-0.115,1.11),(-0.127,-0.13,1.01)],
 'Front_L': [(0.082,-0.088,1.46),(0.085,-0.062,1.39),(0.07,-0.052,1.26)],
}
for cname, pts in hair.items():
    parent = 'CC_Base_Head'
    for i in range(len(pts)-1):
        n = f'Hair_{cname}_{i+1:02d}'
        add(n, pts[i], pts[i+1], parent); parent = n
# earrings
for side, sg, xc in (('L',1,0.078),('R',-1,-0.067)):
    add(f'Earring_{side}_01', (xc,-0.027,1.404), (xc,-0.029,1.372), 'CC_Base_Head')
    add(f'Earring_{side}_02', (xc,-0.029,1.372), (xc,-0.030,1.318), f'Earring_{side}_01')
# kurti skirt chains, sampled from the actual hanging panels (parent hip)
K = co[kurti]
def surface_r(theta, z):
    d = np.array([np.sin(theta), -np.cos(theta)])   # theta 0 = front (-y)
    m = (np.abs(K[:,2]-z) < 0.012)
    P = K[m]
    rel = P[:,:2]
    proj = rel @ d; perp = np.abs(rel @ np.array([d[1], -d[0]]))
    sel = perp < 0.025
    return float(proj[sel].max()) if sel.any() else None
skirt = {'F':0.0,'FL':0.75,'FR':-0.75,'B':np.pi,'BL':np.pi-0.75,'BR':-(np.pi-0.75)}
for cname, th in skirt.items():
    pts = []
    for z in (0.875, 0.775, 0.675):
        r = surface_r(th, z) or 0.14
        r = max(r - 0.012, 0.04)
        pts.append((np.sin(th)*r, -np.cos(th)*r, z))
    parent = 'CC_Base_Hip'
    for i in range(2):
        n = f'Kurti_{cname}_{i+1:02d}'
        add(n, pts[i], pts[i+1], parent); parent = n
# sleeve bells (parent forearm): hanging bottom flap
for side, sg in (('L',1),('R',-1)):
    m = kurti & (sg*co[:,0] > 0.40) & (sg*co[:,0] < 0.49) & (co[:,2] < 1.19)
    P = co[m]
    low = P[np.argsort(P[:,2])[:20]].mean(0)
    top = V((low[0] - sg*0.012, low[1]*0.5, 1.205))
    mid = V((low[0] - sg*0.004, low[1]*0.8, (1.205+low[2])/2))
    bot = V((low[0], low[1], low[2]+0.01))
    add(f'Sleeve_{side}_01', top, mid, f'CC_Base_{side}_Forearm')
    add(f'Sleeve_{side}_02', mid, bot, f'Sleeve_{side}_01')
bpy.app.driver_namespace['rig_bones'] = B
'''
build_code = r'''
import mathutils
B = bpy.app.driver_namespace['rig_bones']
arm_data = bpy.data.armatures.new('Meera_Rig')
rig = bpy.data.objects.new('Meera_Rig', arm_data)
bpy.data.collections['Companion_Work'].objects.link(rig)
arm_data.display_type = 'OCTAHEDRAL'
rig.show_in_front = True
for o in bpy.context.view_layer.objects: o.select_set(False)
bpy.context.view_layer.objects.active = rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
order = list(B.keys())
for n in order:
    h, t, p, d = B[n]
    b = eb.new(n); b.head = h; b.tail = t; b.use_deform = d
for n in order:
    p = B[n][2]
    if p: eb[n].parent = eb[p]
# roll: forward-facing Z on body bones; jaw gets lateral Z for the Unity jaw convention
for b in eb:
    if b.name == 'CC_Base_JawRoot':
        b.align_roll(mathutils.Vector((1,0,0)))
    else:
        b.align_roll(mathutils.Vector((0,-1,0)) if abs(b.vector.normalized().y) < 0.9 else mathutils.Vector((0,0,1)))
# connect chains where head == parent tail
for b in eb:
    if b.parent and (b.head - b.parent.tail).length < 1e-6 and not b.name.startswith('CC_Base_Hand'):
        b.use_connect = True
bpy.ops.object.mode_set(mode='OBJECT')
'''
steps = [{"label": "Measure joints from the mesh", "code": landmark_code},
         {"label": "Build CC-compatible deform skeleton", "code": build_code}]
r = submit("Meera skeleton", steps)
result = {"job": r["job"]["state"]}

# ---- Bone-stick review render (Meera call 51) ----
import bpy, bmesh, mathutils
rig = bpy.data.objects['Meera_Rig']
# build stick visualization
old = bpy.data.objects.get('Review_BoneSticks')
if old: bpy.data.objects.remove(old, do_unlink=True)
bm = bmesh.new()
for b in rig.data.bones:
    h = rig.matrix_world @ b.head_local; t = rig.matrix_world @ b.tail_local
    d = t - h; L = d.length
    if L < 1e-5: continue
    r = 0.0025 if (b.name.startswith('CC_Base') and ('Index' in b.name or 'Mid' in b.name or 'Ring' in b.name or 'Pinky' in b.name or 'Thumb' in b.name or 'Eye' in b.name)) else 0.005
    q = d.to_track_quat('Z','Y')
    ret = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=r*1.6, radius2=r*0.4, depth=L, matrix=mathutils.Matrix.Translation(h + d*0.5) @ q.to_matrix().to_4x4())
me = bpy.data.meshes.new('Review_BoneSticks'); bm.to_mesh(me); bm.free()
st = bpy.data.objects.new('Review_BoneSticks', me)
bpy.data.collections['Review'].objects.link(st)
st.color = (1, 0.15, 0.1, 1)
body = bpy.data.objects['Meera_Body']; body.color = (0.8, 0.8, 0.8, 1)
rig.hide_render = True
sh = bpy.context.scene.display.shading
sh.color_type = 'OBJECT'; sh.show_xray = True; sh.xray_alpha = 0.35; sh.light = 'FLAT'
shoot = bpy.app.driver_namespace['companion_shoot']
a = shoot('rig_front','FRONT', center=(0,0,0.8), ortho=1.75, res=(800,1000))
b = shoot('rig_side','RIGHT', center=(0,0,0.8), ortho=1.75, res=(800,1000))
c = shoot('rig_hand','TOP', center=(0.56,0,1.2), ortho=0.24, res=(800,800))
d = shoot('rig_head','RIGHT', center=(0,-0.02,1.38), ortho=0.35, res=(800,800))
e = shoot('rig_headF','FRONT', center=(0,0,1.38), ortho=0.35, res=(800,800))
sh.show_xray = False; sh.color_type = 'TEXTURE'; sh.light = 'STUDIO'
st.hide_render = True
result = {"ok": 1}
