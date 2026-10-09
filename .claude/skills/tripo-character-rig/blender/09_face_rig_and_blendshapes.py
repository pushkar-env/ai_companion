# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- Solid-colour socket / lip-wall blocks in the mouth texture (Meera call 113) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_walls = r'''
import numpy as np
S = 64; B = 16
tex = np.zeros((S, S, 4), np.float32); tex[...,3] = 1
cols = {'teeth': ((0,0),(0.95,0.93,0.88)), 'gum': ((1,0),(0.66,0.30,0.32)), 'tongue': ((2,0),(0.74,0.38,0.38)),
        'cavity': ((3,0),(0.30,0.075,0.085)), 'socket': ((0,1),(0.07,0.045,0.04)), 'lipwall': ((1,1),(0.42,0.15,0.14))}
tex[...,:3] = (0.30,0.075,0.085)
for k, ((bx, by), cc) in cols.items(): tex[by*B:(by+1)*B, bx*B:(bx+1)*B, :3] = cc
img = bpy.data.images['Meera_Mouth']; img.scale(S, S); img.pixels.foreach_set(tex.ravel()); img.update(); img.save()
def buv(k):
    (bx, by), _ = cols[k]; return ((bx*B + B/2)/S, (by*B + B/2)/S)
# mouth object: remap its part blocks
mo = bpy.data.objects['Meera_Mouth']; mm = mo.data
part = np.empty(len(mm.polygons), np.int32); mm.attributes['mouth_part'].data.foreach_get('value', part)
names = ['teeth','gum','tongue','cavity']
lt = np.empty(len(mm.polygons), np.int32); mm.polygons.foreach_get('loop_total', lt)
uvs = np.array([buv(names[p]) for p in part])
mm.uv_layers.active.data.foreach_set('uv', np.repeat(uvs, lt, axis=0).ravel().astype(np.float32))
# body: socket and lip walls use the mouth material slot
body = bpy.data.objects['Meera_Body']; me = body.data
mat = bpy.data.materials['Meera_Mouth']
if mat.name not in [m.name for m in me.materials if m]: me.materials.append(mat)
slot = [m.name if m else '' for m in me.materials].index(mat.name)
reg = np.empty(len(me.polygons), np.int32); me.attributes['companion_region'].data.foreach_get('value', reg)
mi = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', mi)
mi[reg == 6] = slot; mi[reg == 7] = slot
me.polygons.foreach_set('material_index', mi)
ls = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('loop_start', ls)
blt = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('loop_total', blt)
uvd = np.empty(len(me.loops)*2, np.float32); me.uv_layers.active.data.foreach_get('uv', uvd); uvd = uvd.reshape(-1,2)
for rid, k in ((6, 'socket'), (7, 'lipwall')):
    for fi in np.nonzero(reg == rid)[0]:
        uvd[ls[fi]:ls[fi]+blt[fi]] = buv(k)
me.uv_layers.active.data.foreach_set('uv', uvd.ravel()); me.update()
'''
r = submit("Wall materials", [{"label": "Solid-colour socket and inner lip walls", "code": code_walls}])
result = {"job": r["job"]["state"]}

# ---- Eyelid shells v2 (region 8) + in-between blink frames (Meera call 123) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_reshell = r'''
import bmesh, numpy as np, mathutils
FK = bpy.app.driver_namespace['FK']
body = bpy.data.objects['Meera_Body']; me = body.data
if me.shape_keys: body.shape_key_clear()
nv0, nv1 = bpy.app.driver_namespace['shell_range']
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[bm.verts[i] for i in range(nv0, nv1)], context='VERTS')
bm.to_mesh(me); bm.free(); me.update()
assert len(me.vertices) == nv0
P = np.empty(nv0*3); me.vertices.foreach_get('co', P); P = P.reshape(-1,3)
slots = [m.name if m else '' for m in me.materials]
mouth_slot = slots.index('Meera_Mouth'); skin_slot = slots.index('Meera_Skin')
socket_uv = ((0*16 + 8)/64, (1*16 + 8)/64)
lid_uv = (0.6630102396011353, 0.39944136142730713)
bm = bmesh.new(); bm.from_mesh(me)
uvl = bm.loops.layers.uv.active; reg = bm.faces.layers.int.get('companion_region')
meta = []
for side in ('L', 'R'):
    E = FK['eyes'][side]; c, r = E['c'], E['r']; f, e1, e2 = E['f'], E['e1'], E['e2']
    phi, th = E['phi'][:nv0], E['th'][:nv0]
    rim = np.nonzero(E['rim'][:nv0])[0]
    midc = np.interp(phi[rim], E['pc'], (E['tu'] + E['tl'])/2)
    up_r = rim[th[rim] > midc]; lo_r = rim[th[rim] <= midc]
    def curve(ids):
        o = np.argsort(phi[ids]); p_ = phi[ids][o]; t_ = th[ids][o]
        return p_, np.convolve(np.pad(t_, 2, mode='edge'), np.ones(5)/5, 'valid')
    pu, tu_ = curve(up_r); pl, tl_ = curve(lo_r)
    lo_phi, hi_phi = max(pu[0], pl[0]), min(pu[-1], pl[-1])
    cols = np.linspace(lo_phi - 0.30, hi_phi + 0.30, 36)
    rows_frac = np.r_[0.0, 0.045, np.linspace(0.12, 1.0, 9)]
    grid = []
    for ph in cols:
        pc_ = np.clip(ph, lo_phi, hi_phi)
        tu = np.interp(pc_, pu, tu_); tl = np.interp(pc_, pl, tl_)
        # outside the corners keep the shell tucked above the eye line
        out = max(lo_phi - ph, ph - hi_phi, 0.0)
        th0 = tu + 0.06 + out*0.35
        tc = tl + 0.24*(tu - tl)
        delta = th0 - tc
        H = delta + 0.34
        col = []
        for k, fr in enumerate(rows_frac):
            t_ = th0 + (fr*H if k > 1 else fr)
            p = c + (r + 0.0009)*(np.cos(t_)*(np.sin(ph)*e1 + np.cos(ph)*f) + np.sin(t_)*e2)
            col.append(bm.verts.new(tuple(p))); meta.append((side, float(ph), float(t_), float(delta)))
        grid.append(col)
    for i in range(len(cols)-1):
        for k in range(len(rows_frac)-1):
            fa = bm.faces.new((grid[i][k], grid[i+1][k], grid[i+1][k+1], grid[i][k+1]))
            fa.normal_update()
            if fa.normal.dot(fa.calc_center_median() - mathutils.Vector(c)) < 0: fa.normal_flip()
            fa.smooth = True
            if reg is not None: fa[reg] = 8
            lash = k == 0
            fa.material_index = mouth_slot if lash else skin_slot
            for lp in fa.loops: lp[uvl].uv = socket_uv if lash else lid_uv
bm.normal_update(); bm.to_mesh(me); bm.free(); me.update()
nv1 = len(me.vertices)
bpy.app.driver_namespace['shell_range'] = (nv0, nv1)
bpy.app.driver_namespace['shell_meta'] = meta
body.vertex_groups['CC_Base_Head'].add(list(range(nv0, nv1)), 1.0, 'REPLACE')
jw = bpy.app.driver_namespace['jaw_w'][:nv0]; bpy.app.driver_namespace['jaw_w'] = np.r_[jw, np.zeros(nv1 - nv0)]
lo, up = bpy.app.driver_namespace['lip_sets']
bpy.app.driver_namespace['lip_sets'] = (np.r_[lo[:nv0], np.zeros(nv1 - nv0, bool)], np.r_[up[:nv0], np.zeros(nv1 - nv0, bool)])
'''
code = bpy.app.driver_namespace['code_shapes']
code = code.replace("eye_lids(s, upper=0.12, lower=0.90) + shell(s, 1.08)", "eye_lids(s, upper=0.10, lower=0.45) + shell(s, 1.20)")
# in-between blink frames (merged into multi-frame blendshapes by the Unity import step)
code = code.replace("    D[f'Eye_Widen_{s}'] =",
    "    for fr in (0.25, 0.5, 0.75):\n        D[f'Eye_Blink_{s}__f{int(fr*100)}'] = eye_lids(s, upper=0.10*fr, lower=0.45*fr) + shell(s, 1.20*fr)\n    D[f'Eye_Widen_{s}'] =")
assert "Eye_Blink_{s}__f" in code
bpy.app.driver_namespace['code_shapes'] = code
r = submit("Eyelid shells v2", [{"label": "Rebuild wider eyelid shells", "code": code_reshell},
                                {"label": "Refresh face fields", "code": bpy.app.driver_namespace['code_fields']},
                                {"label": "Recompute displacements", "code": code},
                                {"label": "Write body shape keys", "code": bpy.app.driver_namespace['code_keys']}])
result = {"job": r["job"]["state"]}

# ---- FINAL code_fields (face masks, lip/eye fields) ----
# Stage body for live.submit; stored in bpy.app.driver_namespace under the same name.
code_fields = r'''

import numpy as np, mathutils
FK = {}
body = bpy.data.objects['Meera_Body']; me = body.data
nv = len(me.vertices); nf = len(me.polygons)
P = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', P); P = P.reshape(-1,3)
x, y, z = P[:,0], P[:,1], P[:,2]
ed = np.empty(len(me.edges)*2, np.int32); me.edges.foreach_get('vertices', ed); ed = ed.reshape(-1,2)
lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
fvs = np.empty(len(me.loops), np.int32); me.polygons.foreach_get('vertices', fvs)
reg = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', reg)
fol = np.repeat(np.arange(nf), lt)
vn = np.empty(nv*3); me.vertices.foreach_get('normal', vn); vn = vn.reshape(-1,3)
def ss(e0, e1, v):
    t = np.clip((v - e0)/(e1 - e0), 0, 1); return t*t*(3 - 2*t)
def diffuse(field, mask, iters):
    a, b = ed[:,0], ed[:,1]
    deg = np.zeros(nv); np.add.at(deg, a, 1); np.add.at(deg, b, 1)
    f = field.astype(np.float64).copy()
    for _ in range(iters):
        s = np.zeros(nv); np.add.at(s, a, f[b]); np.add.at(s, b, f[a])
        f = np.where(mask, 0.5*f + 0.5*s/np.maximum(deg, 1), f)
    return f
skinv = np.zeros(nv, bool); skinv[fvs[np.isin(reg[fol], (0, 6, 7))]] = True
face = skinv & (z > 1.30) & (y < 0.0)
ax_, az_ = bpy.app.driver_namespace['lip_curve']
zc = np.interp(np.clip(x, ax_[0], ax_[-1]), ax_, az_)
lower_set, upper_set = bpy.app.driver_namespace['lip_sets']
lo_soft = np.maximum(diffuse(lower_set.astype(float), face & (np.abs(x) < 0.032), 6), lower_set)
up_soft = np.maximum(diffuse(upper_set.astype(float), face & (np.abs(x) < 0.032), 6), upper_set)
cR = np.array([-0.0261, -0.1049, 1.3667]); cL = np.array([0.0260, -0.1083, 1.3719])
mid = (cR + cL)/2
wLowerLip = lo_soft*ss(zc - 0.013, zc - 0.006, z)*face
wUpperLip = up_soft*ss(zc + 0.013, zc + 0.007, z)*face
mouthR = np.linalg.norm((P - np.array([mid[0], -0.115, mid[2]]))*np.array([1.0, 1.0, 1.4]), axis=1)
wMouth = ss(0.050, 0.022, mouthR)*face
FK.update(dict(P=P, nv=nv, vn=vn, face=face, zc=zc, wLowerLip=wLowerLip, wUpperLip=wUpperLip, wMouth=wMouth,
               cR=cR, cL=cL, mid=mid, wj=bpy.app.driver_namespace['jaw_w'], pivot=np.array([0.0, -0.032, 1.405])))
eyes = {}
for side, sg in (('L',1),('R',-1)):
    E = bpy.app.driver_namespace['eye_src_'+side]
    c, r, fwd = E['c'], E['r'], E['f']
    up = np.array([0,0,1.0]); e1 = np.cross(up, fwd); e1 /= np.linalg.norm(e1); e2 = np.cross(fwd, e1)
    near_eye = np.linalg.norm(P - c, axis=1) < r + 0.004
    wallf = (reg == 6)
    wall_v = np.zeros(nv, bool); wall_v[fvs[wallf[fol]]] = True
    other_v = np.zeros(nv, bool); other_v[fvs[(~wallf[fol]) & (reg[fol] != 8)]] = True
    rim = wall_v & other_v & near_eye
    inner = wall_v & ~other_v & near_eye
    d = P - c
    phi = np.arctan2(d @ e1, d @ fwd)
    th = np.arctan2(d @ e2, np.sqrt((d @ e1)**2 + (d @ fwd)**2))
    rp, rt = phi[rim], th[rim]
    bins = np.linspace(rp.min(), rp.max(), 19)
    centers, tu, tl = [], [], []
    for i in range(len(bins)-1):
        m = (rp >= bins[i]) & (rp <= bins[i+1])
        if m.sum() < 2: continue
        centers.append((bins[i]+bins[i+1])/2); tu.append(rt[m].max()); tl.append(rt[m].min())
    centers = np.array(centers); tu = np.array(tu); tl = np.array(tl)
    k = np.array([0.25, 0.5, 0.25])
    tu_s = np.convolve(np.pad(tu, 1, mode='edge'), k, 'valid'); tl_s = np.convolve(np.pad(tl, 1, mode='edge'), k, 'valid')
    eyes[side] = dict(c=c, r=r, f=fwd, e1=e1, e2=e2, phi=phi, th=th, pc=centers, tu=tu_s, tl=tl_s,
                      pmin=rp.min(), pmax=rp.max(), rim=rim, inner=inner)
FK['eyes'] = eyes
bpy.app.driver_namespace['FK'] = FK
'''

# ---- FINAL code_shapes (10 CC visemes, app expressions, 52 ARKit, blink frames) ----
# Stage body for live.submit; stored in bpy.app.driver_namespace under the same name.
code_shapes = r'''

import numpy as np
FK = bpy.app.driver_namespace['FK']
P = FK['P']; nv = FK['nv']; vn = FK['vn']; face = FK['face']; zc = FK['zc']
x, y, z = P[:,0], P[:,1], P[:,2]
wj = FK['wj']; pivot = FK['pivot']; cR, cL, mid = FK['cR'], FK['cL'], FK['mid']
WL, WU, WM = FK['wLowerLip'], FK['wUpperLip'], FK['wMouth']
def ss(e0, e1, v):
    t = np.clip((v - e0)/(e1 - e0), 0, 1); return t*t*(3 - 2*t)
def rotx(Pts, ang, piv=pivot):
    c, s = np.cos(ang), np.sin(ang); R = np.array([[1,0,0],[0,c,-s],[0,s,c]]); return (Pts - piv) @ R.T + piv
def jaw(deg, w=None):
    w = wj if w is None else w
    return (rotx(P, np.radians(deg)) - P) * w[:,None]
def side_w(sg): return ss(-0.006, 0.010, sg*(x - mid[0]))
def corner_w(sg, radius=0.019):
    c = cL if sg > 0 else cR
    d = np.linalg.norm((P - c)*np.array([1.0, 1.0, 1.25]), axis=1)
    return ss(radius, 0.0, d)*face
def vec(vx, vy, vz, w): return np.outer(w, np.array([vx, vy, vz]))
_mr = np.linalg.norm((P - np.array([mid[0], -0.115, mid[2]]))*np.array([1.0, 0.8, 1.35]), axis=1)
lipsw = ss(0.036, 0.011, _mr)*face*ss(zc + 0.017, zc + 0.009, z)   # one smooth field: lips and surrounding skin move together; nose excluded
def pucker(a=1.0):
    w = lipsw*a; d = np.zeros_like(P)
    d[:,0] = -(x - mid[0])*0.24*w; d[:,1] = -0.0048*w; d[:,2] = -(z - zc)*0.14*w
    return d
def lips_part(up=0.001, down=0.001): return vec(0, 0, up, WU) + vec(0, 0, -down, WL)

import heapq
_ed = np.empty(len(bpy.data.objects['Meera_Body'].data.edges)*2, np.int32); bpy.data.objects['Meera_Body'].data.edges.foreach_get('vertices', _ed); _ed = _ed.reshape(-1,2)
_adj = {}
for _a, _b in _ed:
    _adj.setdefault(_a, []).append(_b); _adj.setdefault(_b, []).append(_a)
def _geodesic(seeds, limit):
    dist = {int(s): 0.0 for s in seeds}; pq = [(0.0, int(s)) for s in seeds]; heapq.heapify(pq)
    while pq:
        d, u = heapq.heappop(pq)
        if d > dist.get(u, 1e9) or d > limit: continue
        for v in _adj.get(u, ()):
            nd = d + float(np.linalg.norm(P[u] - P[v]))
            if nd < dist.get(v, 1e9) and nd <= limit: dist[v] = nd; heapq.heappush(pq, (nd, v))
    out = np.full(nv, 1e9)
    for k_, v_ in dist.items(): out[k_] = v_
    return out
_LID = {}
def _lid_setup(side):
    if side in _LID: return _LID[side]
    E = FK['eyes'][side]; c, r = E['c'], E['r']; f, e1, e2 = E['f'], E['e1'], E['e2']
    phi, th = E['phi'], E['th']
    rim = np.nonzero(E['rim'])[0]; inner = np.nonzero(E['inner'])[0]
    midc = np.interp(phi[rim], E['pc'], (E['tu'] + E['tl'])/2)
    up_r = rim[th[rim] > midc]; lo_r = rim[th[rim] <= midc]
    def curve(ids):
        o = np.argsort(phi[ids]); p_ = phi[ids][o]; t_ = th[ids][o]
        t_s = np.convolve(np.pad(t_, 2, mode='edge'), np.ones(5)/5, 'valid')
        return p_, t_s
    pu, tu_ = curve(up_r); pl, tl_ = curve(lo_r)
    lo_phi, hi_phi = max(pu[0], pl[0]), min(pu[-1], pl[-1])
    gu = _geodesic(up_r, 0.016); gl = _geodesic(lo_r, 0.011)
    # inner socket-wall verts follow the closest rim vert
    wall_side = {}
    for v in inner:
        nb = [w_ for w_ in _adj.get(v, ()) if E['rim'][w_]]
        if nb: wall_side[v] = nb[0]
    _LID[side] = dict(E=E, up_r=set(up_r.tolist()), pu=pu, tu=tu_, pl=pl, tl=tl_, lo=lo_phi, hi=hi_phi, gu=gu, gl=gl, wall_side=wall_side)
    return _LID[side]
def eye_lids(side, upper=0.0, lower=0.0, widen=0.0, lower_down=0.0):
    S = _lid_setup(side); E = S['E']
    c, f, e1, e2 = E['c'], E['f'], E['e1'], E['e2']
    phi, th = E['phi'].copy(), E['th']
    gu, gl = S['gu'].copy(), S['gl'].copy()
    for v, rv in S['wall_side'].items():
        if rv in S['up_r']: gu[v] = 0.0; gl[v] = 1e9
        else: gl[v] = 0.0; gu[v] = 1e9
        phi[v] = E['phi'][rv]
    pc = np.clip(phi, S['lo'], S['hi'])
    tu = np.interp(pc, S['pu'], S['tu']); tl = np.interp(pc, S['pl'], S['tl'])
    tc = tl + 0.24*(tu - tl)
    upside = gu <= gl
    wu = np.where(upside, ss(0.0145, 0.0045, gu), 0.0)
    wl = np.where(~upside, ss(0.0095, 0.0025, gl), 0.0)
    span = ss(S['lo'] - 0.02, S['lo'] + 0.10, phi)*ss(S['hi'] + 0.02, S['hi'] - 0.10, phi)
    wu *= np.maximum(span, 0.0); wl *= np.maximum(span, 0.0)
    nth = th - wu*upper*(tu - tc) + wl*lower*(tc - tl) + wu*widen - wl*lower_down
    R = np.linalg.norm(P - c, axis=1)
    newp = c + R[:,None]*(np.cos(nth)[:,None]*(np.sin(E['phi'])[:,None]*e1 + np.cos(E['phi'])[:,None]*f) + np.sin(nth)[:,None]*e2)
    d = newp - P; d[(wu == 0) & (wl == 0)] = 0
    return d

_sr = bpy.app.driver_namespace['shell_range']; _sm = bpy.app.driver_namespace['shell_meta']
def shell(side, amount):
    E = FK['eyes'][side]; c, r, f, e1, e2 = E['c'], E['r'], E['f'], E['e1'], E['e2']
    d = np.zeros_like(P)
    for i, (sd, ph, t_, delta) in enumerate(_sm):
        if sd != side: continue
        vi = _sr[0] + i
        tn = t_ - amount*delta
        p = c + (r + 0.0009)*(np.cos(tn)*(np.sin(ph)*e1 + np.cos(ph)*f) + np.sin(tn)*e2)
        d[vi] = p - P[vi]
    return d
brows = {'L': np.array([[0.017,1.462],[0.040,1.468],[0.066,1.458]]), 'R': np.array([[-0.019,1.462],[-0.034,1.467],[-0.060,1.458]])}
def brow_field(side):
    pts = brows[side]
    best_d = np.full(nv, 1e9); best_t = np.zeros(nv)
    seglen = np.linalg.norm(np.diff(pts, axis=0), axis=1); cum = np.r_[0, np.cumsum(seglen)]
    Q = np.c_[x, z]
    for k in range(len(pts)-1):
        a, dd = pts[k], pts[k+1] - pts[k]
        u = np.clip(((Q - a) @ dd)/(dd @ dd), 0, 1)
        dist = Q - (a + u[:,None]*dd)
        dn = np.sqrt(dist[:,0]**2 + np.where(dist[:,1] > 0, dist[:,1]/2.2, dist[:,1]/0.9)**2)
        better = dn < best_d
        best_d[better] = dn[better]; best_t[better] = (cum[k] + u[better]*seglen[k])/cum[-1]
    return ss(0.016, 0.003, best_d)*face*(y < -0.04), best_t
BW = {s: brow_field(s) for s in ('L','R')}
def brow_up(side, inner=0.0, outer=0.0, amt=0.0045):
    w, t = BW[side]; return vec(0, 0, amt, w*(inner*(1 - t)**1.1 + outer*t**1.1))
def brow_down(side, a=1.0):
    w, t = BW[side]; sg = 1 if side == 'L' else -1
    d = vec(0, -0.0008, -0.0032, w*a); d[:,0] += -sg*0.0013*w*a*(1 - t); return d
cheekC = {'L': np.array([0.047, -0.103, 1.392]), 'R': np.array([-0.047, -0.100, 1.390])}
def cheek_w(side, radius=0.024): return ss(radius, 0.0, np.linalg.norm(P - cheekC[side], axis=1))*face
wingC = {'L': np.array([0.0135, -0.130, 1.390]), 'R': np.array([-0.0135, -0.130, 1.390])}
def wing_w(side, radius=0.013): return ss(radius, 0.0, np.linalg.norm(P - wingC[side], axis=1))*face
def smile(side, a=1.0):
    sg = 1 if side == 'L' else -1
    return vec(0.0032*sg, 0.0013, 0.0040, corner_w(sg, 0.021)*a) + vec(0, -0.0006, 0.0021, cheek_w(side)*a) + eye_lids(side, lower=0.22*a)
def frown(side, a=1.0):
    sg = 1 if side == 'L' else -1; return vec(0.0009*sg, 0.0004, -0.0036, corner_w(sg, 0.020)*a)
def stretch(side, a=1.0):
    sg = 1 if side == 'L' else -1; return vec(0.0036*sg, 0.0012, -0.0012, corner_w(sg, 0.021)*a)
def dimple(side, a=1.0):
    sg = 1 if side == 'L' else -1; return vec(0.0010*sg, 0.0024, 0.0004, corner_w(sg, 0.013)*a)
def press(sg=None, a=1.0):
    s = 1.0 if sg is None else side_w(sg)
    return vec(0, 0.0009, -0.0014, WU*s*a) + vec(0, 0.0009, 0.0014, WL*s*a)
D = {}
D['V_Open'] = jaw(20) + lips_part(0.0008, 0.0)
D['V_Explosive'] = press(a=1.0) + vec(0, 0.0010, 0, (WU + WL))
D['V_Dental_Lip'] = jaw(3) + vec(0, 0.0032, 0.0018, WL)
D['V_Tight_O'] = jaw(6) + pucker(0.85) + lips_part(0.0006, 0.0004)
D['V_Tight'] = jaw(4) + pucker(0.35)
D['V_Wide'] = jaw(6) + stretch('L', 0.7) + stretch('R', 0.7) + smile('L', 0.22) + smile('R', 0.22)
D['V_Affricate'] = jaw(3) + pucker(0.35) + lips_part(0.0012, 0.0010) + vec(0, -0.0015, 0, lipsw)
D['V_Tongue_up'] = jaw(8)
D['V_Tongue_Out'] = jaw(7) + lips_part(0.0008, 0.0006)
D['V_Tongue_Raise'] = jaw(8)
for s in ('L', 'R'):
    D[f'Eye_Blink_{s}'] = eye_lids(s, upper=0.10, lower=0.45) + shell(s, 1.20)
    for fr in (0.25, 0.5, 0.75):
        D[f'Eye_Blink_{s}__f{int(fr*100)}'] = eye_lids(s, upper=0.10*fr, lower=0.45*fr) + shell(s, 1.20*fr)
    D[f'Eye_Widen_{s}'] = eye_lids(s, widen=0.13, lower_down=0.04)
    D[f'Mouth_Corner_Pull_{s}'] = smile(s)
    D[f'Mouth_Corner_Depress_{s}'] = frown(s)
    D[f'Brow_Raise_In_{s}'] = brow_up(s, inner=1.0)
    D[f'Brow_Raise_Outer_{s}'] = brow_up(s, outer=1.0)
    D[f'Brow_Drop_{s}'] = brow_down(s)
A = {}
for s, full in (('L','Left'), ('R','Right')):
    sg = 1 if s == 'L' else -1
    A[f'browDown{full}'] = brow_down(s)
    A[f'browOuterUp{full}'] = brow_up(s, outer=1.0)
    A[f'cheekSquint{full}'] = vec(0, -0.0005, 0.0025, cheek_w(s)) + eye_lids(s, lower=0.35)
    A[f'eyeBlink{full}'] = D[f'Eye_Blink_{s}']
    A[f'eyeSquint{full}'] = eye_lids(s, upper=0.03, lower=0.45) + shell(s, 0.10)
    A[f'eyeWide{full}'] = D[f'Eye_Widen_{s}']
    A[f'eyeLookUp{full}'] = eye_lids(s, widen=0.10)
    A[f'eyeLookDown{full}'] = eye_lids(s, upper=0.05) + shell(s, 0.30)
    A[f'eyeLookIn{full}'] = np.zeros_like(P)
    A[f'eyeLookOut{full}'] = np.zeros_like(P)
    A[f'mouthDimple{full}'] = dimple(s)
    A[f'mouthFrown{full}'] = frown(s)
    A[f'mouthLowerDown{full}'] = vec(0, 0.0004, -0.0032, WL*side_w(sg))
    A[f'mouthPress{full}'] = press(sg)
    A[f'mouthSmile{full}'] = smile(s)
    A[f'mouthStretch{full}'] = stretch(s)
    A[f'mouthUpperUp{full}'] = vec(0, 0.0003, 0.0032, WU*side_w(sg))
    A[f'noseSneer{full}'] = vec(0.0006*sg, -0.0004, 0.0026, wing_w(s)) + vec(0, 0, 0.0008, WU*side_w(sg))
A['browInnerUp'] = brow_up('L', inner=1.0) + brow_up('R', inner=1.0)
A['cheekPuff'] = (vn*0.0042)*(np.maximum(cheek_w('L', 0.03), cheek_w('R', 0.03))*ss(1.40, 1.385, z))[:,None] + vec(0, -0.0008, 0, lipsw*0.5)
A['jawForward'] = vec(0, -0.0040, 0, wj)
A['jawLeft'] = vec(0.0042, 0, 0, wj)
A['jawRight'] = vec(-0.0042, 0, 0, wj)
A['jawOpen'] = jaw(22)
A['mouthClose'] = -(rotx(P, np.radians(22)) - P)*np.minimum(WL*1.15, 1)[:,None]
A['mouthFunnel'] = pucker(0.65) + lips_part(0.0022, 0.0022) + vec(0, -0.0020, 0, lipsw)
A['mouthPucker'] = pucker(1.0)
A['mouthLeft'] = vec(0.0050, 0, 0, WM)
A['mouthRight'] = vec(-0.0050, 0, 0, WM)
A['mouthRollLower'] = vec(0, 0.0036, 0.0016, WL)
A['mouthRollUpper'] = vec(0, 0.0036, -0.0016, WU)
A['mouthShrugLower'] = vec(0, -0.0010, 0.0020, WL) + vec(0, -0.0006, 0.0014, wj*ss(1.36, 1.335, z))
A['mouthShrugUpper'] = vec(0, -0.0004, 0.0022, WU)
A['tongueOut'] = jaw(6)
D.update(A)
bpy.app.driver_namespace['shape_disp'] = D
bpy.app.driver_namespace['jaw_deg'] = {'V_Open':20,'V_Dental_Lip':3,'V_Tight_O':7,'V_Tight':4,'V_Wide':6,'V_Affricate':3,'V_Tongue_up':8,'V_Tongue_Out':7,'V_Tongue_Raise':8,'jawOpen':22,'tongueOut':6}
'''

# ---- FINAL code_keys (write body shape keys) ----
# Stage body for live.submit; stored in bpy.app.driver_namespace under the same name.
code_keys = r'''

import numpy as np
D = bpy.app.driver_namespace['shape_disp']
FK = bpy.app.driver_namespace['FK']; P = FK['P']
body = bpy.data.objects['Meera_Body']
if body.data.shape_keys: body.shape_key_clear()
body.shape_key_add(name='Basis', from_mix=False)
for name, d in D.items():
    k = body.shape_key_add(name=name, from_mix=False)
    k.data.foreach_set('co', (P + d).ravel().astype(np.float32))
    k.slider_min = 0.0; k.slider_max = 1.0
'''

# ---- FINAL code_parts (mouth jaw/tongue + eyeball look shapes) ----
# Stage body for live.submit; stored in bpy.app.driver_namespace under the same name.
code_parts = r'''

import numpy as np
pivot = np.array([0.0, -0.032, 1.405])
def rotx(Pts, ang):
    c, s = np.cos(ang), np.sin(ang); R = np.array([[1,0,0],[0,c,-s],[0,s,c]]); return (Pts - pivot) @ R.T + pivot
def ss(e0, e1, v):
    t = np.clip((v - e0)/(e1 - e0), 0, 1); return t*t*(3 - 2*t)
mo = bpy.data.objects['Meera_Mouth']; mm = mo.data
mv = len(mm.vertices)
M = np.empty(mv*3, np.float64); mm.vertices.foreach_get('co', M); M = M.reshape(-1,3)
mw = bpy.app.driver_namespace['mouth_jaw_w']; vpart = bpy.app.driver_namespace['mouth_vpart']
tongue = (vpart == 2).astype(float)
jd = bpy.app.driver_namespace['jaw_deg']
MD = {}
for name, deg in jd.items():
    MD[name] = (rotx(M, np.radians(deg)) - M)*mw[:,None]
ty = M[:,1]
MD['V_Tongue_up'] = MD['V_Tongue_up'] + np.outer(tongue*ss(-0.080, -0.104, ty), [0, -0.0020, 0.0065])
MD['V_Tongue_Out'] = MD['V_Tongue_Out'] + np.outer(tongue*ss(-0.074, -0.104, ty), [0, -0.0105, 0.0025])
MD['V_Tongue_Raise'] = MD['V_Tongue_Raise'] + np.outer(tongue*ss(-0.090, -0.071, ty), [0, 0, 0.0055])
MD['tongueOut'] = MD['tongueOut'] + np.outer(tongue*ss(-0.072, -0.104, ty), [0, -0.0140, -0.0010])
if mm.shape_keys: mo.shape_key_clear()
mo.shape_key_add(name='Basis', from_mix=False)
for name, d in MD.items():
    k = mo.shape_key_add(name=name, from_mix=False); k.data.foreach_set('co', (M + d).ravel().astype(np.float32))
# eyeballs: ARKit look directions
eo = bpy.data.objects['Meera_Eyes']; em = eo.data
ev = len(em.vertices)
Q = np.empty(ev*3, np.float64); em.vertices.foreach_get('co', Q); Q = Q.reshape(-1,3)
def rot(Pts, c, axis, ang):
    axis = axis/np.linalg.norm(axis); v = Pts - c
    cos, sin = np.cos(ang), np.sin(ang)
    return c + v*cos + np.cross(axis, v)*sin + np.outer(v @ axis, axis)*(1 - cos)
ED = {}
for side, full in (('L','Left'), ('R','Right')):
    E = bpy.app.driver_namespace['eye_src_'+side]; c, f = E['c'], E['f']
    up = np.array([0,0,1.0]); e1 = np.cross(up, f); e1 /= np.linalg.norm(e1); e2 = np.cross(f, e1)
    m = (np.sign(Q[:,0]) == (1 if side == 'L' else -1))
    lateral = e1 if side == 'L' else -e1
    def look(axis, deg):
        d = np.zeros_like(Q); d[m] = rot(Q[m], c, axis, np.radians(deg)) - Q[m]; return d
    ED[f'eyeLookUp{full}'] = look(np.cross(f, e2), 20)
    ED[f'eyeLookDown{full}'] = look(np.cross(f, -e2), 22)
    ED[f'eyeLookOut{full}'] = look(np.cross(f, lateral), 25)
    ED[f'eyeLookIn{full}'] = look(np.cross(f, -lateral), 22)
if em.shape_keys: eo.shape_key_clear()
eo.shape_key_add(name='Basis', from_mix=False)
for name, d in ED.items():
    k = eo.shape_key_add(name=name, from_mix=False); k.data.foreach_set('co', (Q + d).ravel().astype(np.float32))
'''
