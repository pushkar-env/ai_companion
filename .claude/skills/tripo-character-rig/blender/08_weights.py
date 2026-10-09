# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- Bone heat on core skeleton (dynamic/face bones excluded) (Meera call 81) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_auto = r'''
rig = bpy.data.objects['Meera_Rig']; body = bpy.data.objects['Meera_Body']
excluded = []
for b in rig.data.bones:
    n = b.name
    if n.startswith(('Hair_','Earring_','Kurti_','Sleeve_')) or n in ('CC_Base_JawRoot','CC_Base_L_Eye','CC_Base_R_Eye','CC_Base_BoneRoot'):
        if b.use_deform: excluded.append(n); b.use_deform = False
bpy.app.driver_namespace['excluded_deform'] = excluded
for o in bpy.context.view_layer.objects: o.select_set(False)
body.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
for n in excluded: rig.data.bones[n].use_deform = True
'''
r = submit("Skinning (bone heat)", [{"label": "Automatic weights on core skeleton", "code": code_auto}])
result = {"job": r["job"]["state"]}

# ---- Vertex regions + auto weights snapshot (Meera call 83) ----
import bpy, numpy as np, mathutils
from mathutils.kdtree import KDTree
body = bpy.data.objects['Meera_Body']; me = body.data; rig = bpy.data.objects['Meera_Rig']
nv = len(me.vertices); nf = len(me.polygons)
co = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
bones = [b.name for b in rig.data.bones]
bidx = {n:i for i,n in enumerate(bones)}
Wt = np.zeros((nv, len(bones)), np.float64)
gname = {g.index: g.name for g in body.vertex_groups}
for v in me.vertices:
    for g in v.groups:
        n = gname[g.group]
        if n in bidx: Wt[v.index, bidx[n]] = g.weight
tot = Wt.sum(1)
lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
fvs = np.empty(len(me.loops), np.int32); me.polygons.foreach_get('vertices', fvs)
reg = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', reg)
fol = np.repeat(np.arange(nf), lt)
# vertex region = majority/any priority
vreg = np.full(nv, -1)
for r_ in (2,3,4,0,1,5,6,7):   # later overrides earlier
    vreg[fvs[reg[fol]==r_]] = r_
bpy.app.driver_namespace['vreg'] = vreg
bpy.app.driver_namespace['W_auto'] = Wt.copy()
bpy.app.driver_namespace['bone_list'] = bones
unweighted = int((tot < 1e-6).sum())
top = {}
for r_ in range(8):
    m = vreg == r_
    if m.any():
        s = Wt[m].sum(0); order = np.argsort(-s)[:5]
        top[r_] = [(bones[i], round(float(s[i]/max(m.sum(),1)),3)) for i in order]
result = {"unweighted": unweighted, "regions": {int(k): int((vreg==k).sum()) for k in range(-1,8)}, "top": top}

# ---- FINAL code_w2: shoulder blend + smooth chin blend + jaw split ----
# Stage body for live.submit; stored in bpy.app.driver_namespace under the same name.
code_w2 = r'''

import numpy as np, mathutils
from mathutils.kdtree import KDTree
body = bpy.data.objects['Meera_Body']; me = body.data; rig = bpy.data.objects['Meera_Rig']
nv = len(me.vertices)
co = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
x, y, z = co[:,0], co[:,1], co[:,2]
ed = np.empty(len(me.edges)*2, np.int32); me.edges.foreach_get('vertices', ed); ed = ed.reshape(-1,2)
bones = bpy.app.driver_namespace['bone_list']; B = {n:i for i,n in enumerate(bones)}
W = bpy.app.driver_namespace['W_auto'].copy()
vreg = bpy.app.driver_namespace['vreg']; wj = bpy.app.driver_namespace['jaw_w']
def ss(e0, e1, v):
    t = np.clip((v - e0)/(e1 - e0), 0, 1); return t*t*(3 - 2*t)
H, J = B['CC_Base_Head'], B['CC_Base_JawRoot']
# 1) shoulders: wider clavicle -> upper-arm transition
for side, sg in (('L',1),('R',-1)):
    U, C, S = B[f'CC_Base_{side}_Upperarm'], B[f'CC_Base_{side}_Clavicle'], B['CC_Base_Spine02']
    ax_ = sg*x
    m = (ax_ > 0.07) & (ax_ < 0.28) & (z > 1.08) & ((vreg == 0) | (vreg == 2))
    lat = ss(0.13, 0.17, ax_[m])
    mass = W[m, U] + W[m, C] + W[m, S]*lat
    ut = ss(0.105, 0.215, ax_[m])
    ut = np.maximum(ut, W[m, U]/np.maximum(mass, 1e-9))
    W[m, S] *= (1 - lat)
    W[m, U] = mass*ut; W[m, C] = mass*(1 - ut)
    # weight smoothing in the shoulder band
    region = (ax_ > 0.06) & (ax_ < 0.30) & (z > 1.06) & ((vreg == 0) | (vreg == 2))
    a, b = ed[:,0], ed[:,1]
    keep = region[a] & region[b]
    a, b = a[keep], b[keep]
    deg = np.zeros(nv); np.add.at(deg, a, 1); np.add.at(deg, b, 1)
    for it in range(8):
        acc = np.zeros_like(W[:, [U, C, S]])
        np.add.at(acc, a, W[b][:, [U, C, S]]); np.add.at(acc, b, W[a][:, [U, C, S]])
        avg = acc/np.maximum(deg, 1)[:,None]
        sel = region & (deg > 0)
        cur = W[sel][:, [U, C, S]]
        new = 0.5*cur + 0.5*avg[sel]
        tot_old = cur.sum(1); tot_new = np.maximum(new.sum(1), 1e-9)
        new *= (tot_old/tot_new)[:,None]
        Wsel = W[sel]; Wsel[:, [U, C, S]] = new; W[sel] = Wsel
# 2) face: smooth blend from neck weights to head-only, then split jaw
facey = (vreg == 0) | (vreg == 6) | (vreg == 7)
f = np.where(y < -0.03, ss(1.318, 1.352, z), ss(1.345, 1.378, z))
f = np.where(facey, f, 0)
headonly = np.zeros_like(W); headonly[:, H] = 1
W = W*(1 - f)[:,None] + headonly*f[:,None]
nz = facey & (z > 1.27)
h = W[nz, H].copy(); W[nz, J] = h*wj[nz]; W[nz, H] = h*(1 - wj[nz])
bpy.app.driver_namespace['W_auto_fixed'] = W
'''

# ---- FINAL code_w_rest: earrings, hair chains + body attach (lower arm excluded), kurti, sleeves, 4-influence limit ----
# Stage body for live.submit; stored in bpy.app.driver_namespace under the same name.
code_w_rest = r'''

import numpy as np, mathutils
from mathutils.kdtree import KDTree
body = bpy.data.objects['Meera_Body']; me = body.data; rig = bpy.data.objects['Meera_Rig']
nv = len(me.vertices)
co = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
x, y, z = co[:,0], co[:,1], co[:,2]
bones = bpy.app.driver_namespace['bone_list']; B = {n:i for i,n in enumerate(bones)}
W = bpy.app.driver_namespace['W_auto_fixed'].copy()
vreg = bpy.app.driver_namespace['vreg']
def ss(e0, e1, v):
    t = np.clip((v - e0)/(e1 - e0), 0, 1); return t*t*(3 - 2*t)
H = B['CC_Base_Head']
arm_bones = [i for n,i in B.items() if any(k in n for k in ('Upperarm','Forearm','_Hand','Thumb','Index','Mid','Ring','Pinky'))]
# earrings
ear = vreg == 5
for side, sg in (('L', 1), ('R', -1)):
    m = ear & (np.sign(x) == sg); rid = np.nonzero(m)[0]; zz = z[m]
    W[m] = 0
    hb = ss(1.394, 1.402, zz); e1 = ss(1.364, 1.378, zz)*(1 - hb); e2 = 1 - hb - e1
    W[rid, H] = hb; W[rid, B[f'Earring_{side}_01']] = e1; W[rid, B[f'Earring_{side}_02']] = e2
# hair chains
chains = {}
for b in rig.data.bones:
    if b.name.startswith('Hair_'): chains.setdefault(b.name.rsplit('_', 1)[0], []).append(b)
chain_data = []
for cname, bl in chains.items():
    bl.sort(key=lambda b: b.name)
    pts = [np.array(bl[0].head_local)] + [np.array(b.tail_local) for b in bl]
    chain_data.append((cname, [B[b.name] for b in bl], np.array(pts)))
hair = vreg == 1; hv = co[hair]
D = []; KU = []
for cname, bids, P in chain_data:
    bd = np.full(len(hv), 1e9); bk = np.zeros(len(hv), int); bu = np.zeros(len(hv))
    for k in range(len(P)-1):
        a, d = P[k], P[k+1]-P[k]
        u = np.clip(((hv - a) @ d)/(d @ d), 0, 1)
        dist = np.linalg.norm(hv - (a + u[:,None]*d), axis=1)
        bt = dist < bd; bd[bt] = dist[bt]; bk[bt] = k; bu[bt] = u[bt]
    D.append(bd); KU.append((bk, bu))
D = np.array(D); order = np.argsort(D, axis=0)[:2]
c0, c1 = order[0], order[1]
w0 = 1.0/(D[c0, np.arange(len(hv))] + 0.012)**2; w1 = 1.0/(D[c1, np.arange(len(hv))] + 0.012)**2
tw = w0 + w1
Wh = np.zeros((len(hv), len(bones)))
for ci, ww in ((c0, w0/tw), (c1, w1/tw)):
    for c_idx, (cname, bids, P) in enumerate(chain_data):
        m = ci == c_idx
        if not m.any(): continue
        k, u = KU[c_idx][0][m], KU[c_idx][1][m]; n = len(bids); rws = np.nonzero(m)[0]
        wk = np.where(u < 0.5, np.where(k > 0, 0.5 + u, 1.0), np.where(k < n-1, 1.5 - u, 1.0))
        nb = np.clip(np.where(u < 0.5, k - 1, k + 1), 0, n-1)
        np.add.at(Wh, (rws, np.array(bids)[k]), ww[m]*wk)
        np.add.at(Wh, (rws, np.array(bids)[nb]), ww[m]*(1 - wk))
root_z = np.array([P[0][2] for _, _, P in chain_data]); rz = root_z[c0]
headw = np.maximum(ss(rz - 0.03, rz + 0.012, hv[:,2]), ss(1.43, 1.47, hv[:,2]))
Wh = Wh*(1 - headw)[:,None]; Wh[:, H] += headw
nonhair = np.nonzero((vreg != 1) & (vreg != 5))[0]
kd = KDTree(len(nonhair))
for i, vi in enumerate(nonhair): kd.insert(co[vi], i)
kd.balance()
dist_b = np.zeros(len(hv)); near = np.zeros(len(hv), int)
for i, p in enumerate(hv):
    _, idx, d = kd.find(p); dist_b[i] = d; near[i] = nonhair[idx]
Wb = W[near].copy()
lower_arm = [i for n,i in B.items() if any(k in n for k in ('Forearm','_Hand','Thumb','Index','Mid','Ring','Pinky'))]
Wb[:, lower_arm] = 0
sb = Wb.sum(1); Wb[sb < 1e-6, B['CC_Base_Spine02']] = 1; Wb /= Wb.sum(1)[:,None]
battach = ss(0.026, 0.005, dist_b)*(1 - headw)
W[hair] = Wh*(1 - battach)[:,None] + Wb*battach[:,None]
# kurti panels
kur = (vreg == 2) & (z < 0.905) & (np.abs(x) < 0.26)
th = np.arctan2(x[kur], -y[kur])
names = ['F','FL','BL','B','BR','FR']; angs = np.array([0.0, 0.75, np.pi-0.75, np.pi, -(np.pi-0.75), -0.75])
Dk = np.array([np.abs((th - a + np.pi) % (2*np.pi) - np.pi) for a in angs])
o2 = np.argsort(Dk, axis=0)[:2]
da = Dk[o2[0], np.arange(len(th))]; db = Dk[o2[1], np.arange(len(th))]
wa = db/(da + db + 1e-9); wb = 1 - wa
zz = z[kur]; w01 = np.clip(1 - (0.825 - zz)/0.10, 0, 1); w02 = 1 - w01
infl = ss(0.905, 0.805, zz)
rws = np.nonzero(kur)[0]; Wk = W[rws]*(1 - infl)[:,None]
for oi, ww in ((o2[0], wa), (o2[1], wb)):
    for j, nm in enumerate(names):
        m = oi == j
        if m.any():
            Wk[m, B[f'Kurti_{nm}_01']] += (infl*ww*w01)[m]; Wk[m, B[f'Kurti_{nm}_02']] += (infl*ww*w02)[m]
W[rws] = Wk
# sleeve bells
for side, sg in (('L',1),('R',-1)):
    sl = (vreg == 2) & (sg*x > 0.385) & (sg*x < 0.51) & (z > 1.0)
    zz = z[sl]; xx = sg*x[sl]
    infl = ss(1.205, 1.165, zz)*ss(0.385, 0.425, xx); w1 = 1 - np.clip((1.177 - zz)/0.045, 0, 1)
    rws = np.nonzero(sl)[0]; Ws = W[rws]*(1 - infl)[:,None]
    Ws[:, B[f'Sleeve_{side}_01']] += infl*w1; Ws[:, B[f'Sleeve_{side}_02']] += infl*(1 - w1)
    W[rws] = Ws
idx = np.argsort(-W, axis=1)[:, :4]
Wl = np.zeros_like(W); r_ = np.arange(nv)[:,None]
Wl[r_, idx] = W[r_, idx]; Wl[Wl < 0.01] = 0
Wl /= np.maximum(Wl.sum(1), 1e-9)[:,None]
bpy.app.driver_namespace['W_final'] = Wl
'''

# ---- FINAL code_w_write ----
# Stage body for live.submit; stored in bpy.app.driver_namespace under the same name.
code_w_write = r'''

import numpy as np
body = bpy.data.objects['Meera_Body']
Wl = bpy.app.driver_namespace['W_final']; bones = bpy.app.driver_namespace['bone_list']
for g in list(body.vertex_groups): body.vertex_groups.remove(g)
for j, n in enumerate(bones):
    col = Wl[:, j]; nz = np.nonzero(col > 0)[0]
    if len(nz) == 0: continue
    vg = body.vertex_groups.new(name=n)
    q = np.round(col[nz]*1000).astype(int)
    for val in np.unique(q): vg.add(nz[q == val].tolist(), val/1000.0, 'REPLACE')
bpy.context.view_layer.update()
'''

# ---- Bind eyes/mouth + relaxed test pose ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_bind = r'''
import numpy as np
rig = bpy.data.objects['Meera_Rig']
eyes = bpy.data.objects['Meera_Eyes']; me = eyes.data
co = np.empty(len(me.vertices)*3); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
for side, sg in (('L',1),('R',-1)):
    vg = eyes.vertex_groups.get(f'CC_Base_{side}_Eye') or eyes.vertex_groups.new(name=f'CC_Base_{side}_Eye')
    vg.add(np.nonzero(np.sign(co[:,0]) == sg)[0].tolist(), 1.0, 'REPLACE')
mouth = bpy.data.objects['Meera_Mouth']; mm = mouth.data
w = bpy.app.driver_namespace['mouth_jaw_w']
gh = mouth.vertex_groups.new(name='CC_Base_Head'); gj = mouth.vertex_groups.new(name='CC_Base_JawRoot')
q = np.round(w*1000).astype(int)
for val in np.unique(q):
    ids = np.nonzero(q == val)[0].tolist()
    if val < 1000: gh.add(ids, 1 - val/1000.0, 'REPLACE')
    if val > 0: gj.add(ids, val/1000.0, 'REPLACE')
for ob in (eyes, mouth):
    ob.parent = rig
    md = ob.modifiers.new('Armature', 'ARMATURE'); md.object = rig
'''
code_pose = r'''
import math, mathutils
rig = bpy.data.objects['Meera_Rig']
pb = rig.pose.bones
def rot_world(name, axis, deg):
    b = pb[name]
    M = rig.matrix_world @ b.matrix
    R = mathutils.Matrix.Rotation(math.radians(deg), 4, axis)
    loc = M.to_translation()
    newM = mathutils.Matrix.Translation(loc) @ R @ mathutils.Matrix.Translation(-loc) @ M
    b.matrix = rig.matrix_world.inverted() @ newM
    bpy.context.view_layer.update()
rot_world('CC_Base_L_Upperarm', 'Y', 68)
rot_world('CC_Base_R_Upperarm', 'Y', -68)
rot_world('CC_Base_L_Forearm', 'X', 18)
rot_world('CC_Base_R_Forearm', 'X', 18)
rot_world('CC_Base_Head', 'Z', 14)
rot_world('CC_Base_JawRoot', 'X', 9)
'''
r = submit("Bind and test pose", [{"label": "Bind eyes and mouth parts", "code": code_bind},
                                   {"label": "Relaxed test pose", "code": code_pose}])
result = {"job": r["job"]["state"]}
