# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- RANSAC sphere fit of painted eye bulges (Meera call 45) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
nv = len(me.vertices)
co = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
rng = np.random.default_rng(7)
def fit_sphere(P):
    A = np.c_[2*P, np.ones(len(P))]; b = (P**2).sum(1)
    sol, *_ = np.linalg.lstsq(A, b, rcond=None)
    c = sol[:3]; return c, float(np.sqrt(max(sol[3] + c@c, 1e-12)))
out = {}
for side, cx, cz in (('L', 0.040, 1.434), ('R', -0.038, 1.433)):
    m = (((co[:,0]-cx)/0.024)**2 + ((co[:,2]-cz)/0.017)**2 < 1) & (co[:,1] < -0.06)
    P = co[m]
    best = None
    for it in range(4000):
        idx = rng.choice(len(P), 4, replace=False)
        try: c, r = fit_sphere(P[idx])
        except Exception: continue
        if not (0.010 < r < 0.035) or c[1] < P[:,1].min(): continue
        d = np.abs(np.linalg.norm(P - c, axis=1) - r)
        n = int((d < 0.0005).sum())
        if best is None or n > best[0]: best = (n, c, r)
    n, c, r = best
    d = np.abs(np.linalg.norm(P - c, axis=1) - r); inl = d < 0.0007
    c, r = fit_sphere(P[inl])
    d = np.linalg.norm(P - c, axis=1) - r; inl = np.abs(d) < 0.0007
    Q = P[inl]
    out[side] = {"cand": len(P), "inliers": int(inl.sum()), "center": [round(float(v),4) for v in c], "r": round(r,4),
                 "inl_x": [round(float(Q[:,0].min()),4), round(float(Q[:,0].max()),4)], "inl_z": [round(float(Q[:,2].min()),4), round(float(Q[:,2].max()),4)], "inl_y": [round(float(Q[:,1].min()),4), round(float(Q[:,1].max()),4)]}
    bpy.app.driver_namespace['eye_fit_'+side] = (c, r)
result = out

# ---- Eye surface patch faces (Meera call 52) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
nv = len(me.vertices); nf = len(me.polygons)
co = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
fv = np.empty(nf*3, np.int32); me.polygons.foreach_get('vertices', fv); fv = fv.reshape(-1,3)
fn = np.empty(nf*3, np.float64); me.polygons.foreach_get('normal', fn); fn = fn.reshape(-1,3)
fc = np.empty(nf*3, np.float64); me.polygons.foreach_get('center', fc); fc = fc.reshape(-1,3)
indptr, nbr = bpy.app.driver_namespace['face_csr']
out = {}
patch_all = np.zeros(nf, bool)
for side in ('L','R'):
    c, r = bpy.app.driver_namespace['eye_fit_'+side]
    c = np.array(c)
    dv = np.linalg.norm(co - c, axis=1) - r
    onsphere_v = np.abs(dv) < 0.0009
    radial = (fc - c); radial /= np.linalg.norm(radial, axis=1)[:,None]
    facing = (fn * radial).sum(1) > 0.6
    cand = onsphere_v[fv].all(1) & facing & (fc[:,1] < c[1] - 0.004) & (np.linalg.norm(fc - c, axis=1) < r + 0.003)
    # largest connected component of candidates
    comp = np.where(cand, np.arange(nf), -1)
    a = np.repeat(np.arange(nf), np.diff(indptr))
    e = cand[a] & cand[nbr]
    src, dst = a[e], nbr[e]
    for it in range(500):
        newc = comp.copy(); np.minimum.at(newc, src, comp[dst])
        if (newc == comp).all(): break
        comp = newc
    ids, cnt = np.unique(comp[cand], return_counts=True)
    best = ids[np.argmax(cnt)]
    patch = cand & (comp == best)
    # fill small holes: faces whose 3 neighbours are mostly patch
    for it in range(3):
        votes = np.zeros(nf, np.int32); np.add.at(votes, a, patch[nbr].astype(np.int32))
        add = (~patch) & (votes >= 2) & (np.linalg.norm(fc - c, axis=1) < r + 0.002) & facing
        if not add.any(): break
        patch |= add
    patch_all |= patch
    P = fc[patch]
    out[side] = {"faces": int(patch.sum()), "comps": int(len(ids)), "x": [round(float(P[:,0].min()),4), round(float(P[:,0].max()),4)], "z": [round(float(P[:,2].min()),4), round(float(P[:,2].max()),4)]}
bpy.app.driver_namespace['eye_patch_faces'] = patch_all
# boundary check: edges used by exactly one patch face
le = np.empty(len(me.loops), np.int32); me.loops.foreach_get('edge_index', le)
lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
fol = np.repeat(np.arange(nf), lt)
pe = le[patch_all[fol]]
ue, ec = np.unique(pe, return_counts=True)
out["boundary_edges"] = int((ec == 1).sum())
# visualize
PAL = np.array([[0.8,0.8,0.8],[1,0,0]], np.float32)
attr = me.color_attributes.get('debug_seg')
attr.data.foreach_set('color', np.repeat(np.c_[PAL[patch_all.astype(int)], np.ones(nf, np.float32)], lt, axis=0).ravel())
me.color_attributes.active_color = attr
sh = bpy.context.scene.display.shading; sh.color_type='VERTEX'; sh.light='STUDIO'
shoot = bpy.app.driver_namespace['companion_shoot']
p = shoot('eyepatch','FRONT', center=(0,0,1.43), ortho=0.14, res=(900,900))
sh.color_type='TEXTURE'
result = out

# ---- Iris axis / sclera sampling (Meera call 53) ----
import bpy, numpy as np, mathutils
from mathutils.bvhtree import BVHTree
ob = bpy.data.objects['Meera_Body']; me = ob.data
nv = len(me.vertices); nf = len(me.polygons)
co = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
fv = np.empty(nf*3, np.int32); me.polygons.foreach_get('vertices', fv); fv = fv.reshape(-1,3)
uv = np.empty(len(me.loops)*2, np.float64); me.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1,2)
ls = np.empty(nf, np.int32); me.polygons.foreach_get('loop_start', ls)
patch = bpy.app.driver_namespace['eye_patch_faces']
img = bpy.data.images['Meera_BaseColor']; W, H = img.size
px = np.empty(W*H*4, np.float32); img.pixels.foreach_get(px); px = px.reshape(H, W, 4)
def sample(u, v):
    x = np.clip(u*W - 0.5, 0, W-1.001); y = np.clip(v*H - 0.5, 0, H-1.001)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); fx = (x-x0)[...,None]; fy = (y-y0)[...,None]
    c00 = px[y0, x0, :3]; c10 = px[y0, x0+1, :3]; c01 = px[y0+1, x0, :3]; c11 = px[y0+1, x0+1, :3]
    return (c00*(1-fx)+c10*fx)*(1-fy) + (c01*(1-fx)+c11*fx)*fy
info = {}
for side in ('L','R'):
    c, r = bpy.app.driver_namespace['eye_fit_'+side]; c = np.array(c)
    sel = patch & (np.sign(co[fv[:,0],0]) == (1 if side=='L' else -1))
    fidx = np.nonzero(sel)[0]
    tri_co = co[fv[fidx]]; tri_uv = uv[ls[fidx][:,None] + np.arange(3)[None,:]]
    # iris center = centroid of dark texels on the patch
    cent = tri_co.mean(1); cuv = tri_uv.mean(1)
    col = sample(cuv[:,0], cuv[:,1]); L = col @ np.array([0.299,0.587,0.114])
    dark = L < np.percentile(L, 35)
    iris_c = cent[dark].mean(0)
    f = iris_c - c; f /= np.linalg.norm(f)
    # sclera colour: bright, low-saturation patch texels
    sat = col.max(1) - col.min(1)
    scl = col[(L > np.percentile(L, 80)) & (sat < np.percentile(sat, 60))]
    sclera = np.median(scl, 0) if len(scl) else np.array([0.85,0.82,0.80])
    # iris angular radius: angle where dark/brown texels stop
    ang = np.degrees(np.arccos(np.clip(((cent - c)/np.linalg.norm(cent-c,axis=1)[:,None]) @ f, -1, 1)))
    brownish = L < (np.median(L[dark]) + 0.6*(np.median(scl @ np.array([0.299,0.587,0.114])) - np.median(L[dark]))) if len(scl) else dark
    iris_deg = float(np.percentile(ang[brownish], 92))
    info[side] = {"axis": [round(float(v),4) for v in f], "iris_deg": round(iris_deg,2), "sclera": [round(float(v),3) for v in sclera], "patch_faces": int(len(fidx))}
    bpy.app.driver_namespace['eye_src_'+side] = dict(c=c, r=r, f=f, tri_co=tri_co, tri_uv=tri_uv, sclera=sclera, iris_deg=iris_deg)
result = info

# ---- Rotatable eyeball spheres with polar UVs (Meera call 56) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
assert bpy.data.objects.get('Meera_Eyes') is None
code_mesh = r'''
import bmesh, numpy as np, mathutils
bm = bmesh.new()
uvl = bm.loops.layers.uv.new('UVMap')
for k, side in enumerate(('L','R')):
    E = bpy.app.driver_namespace['eye_src_'+side]
    c, r, f = E['c'], E['r'], E['f']
    up = np.array([0,0,1.0]); e1 = np.cross(up, f); e1 /= np.linalg.norm(e1); e2 = np.cross(f, e1)
    rot = mathutils.Matrix((tuple(e1), tuple(e2), tuple(f))).transposed()
    mat = mathutils.Matrix.Translation(mathutils.Vector(c)) @ rot.to_4x4()
    before = set(bm.faces)
    bmesh.ops.create_uvsphere(bm, u_segments=36, v_segments=24, radius=r - 0.0002, matrix=mat)
    for fa in [fa for fa in bm.faces if fa not in before]:
        for lp in fa.loops:
            d = np.array(lp.vert.co) - c; d /= np.linalg.norm(d)
            a = np.arccos(np.clip(d @ f, -1, 1)); b = np.arctan2(d @ e2, d @ e1)
            rho = a/np.pi
            lp[uvl].uv = ((0.5 + 0.5*rho*np.cos(b) + k)*0.5, 0.5 + 0.5*rho*np.sin(b))
me = bpy.data.meshes.new('Meera_Eyes')
bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth = True
eo = bpy.data.objects.new('Meera_Eyes', me)
bpy.data.collections['Companion_Work'].objects.link(eo)
mat = bpy.data.materials.get('Meera_Eyes') or bpy.data.materials.new('Meera_Eyes')
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes.get('Principled BSDF')
tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images['Meera_Eyes']
nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value = 0.12
me.materials.append(mat)
'''
r = submit("Eyeballs", [{"label": "Build rotatable eyeballs", "code": code_mesh}])
result = {"job": r["job"]["state"]}

# ---- Eye texture from radial profile (superseded by procedural iris below) (Meera call 57) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_tex = r'''
import numpy as np, mathutils
from mathutils.bvhtree import BVHTree
img = bpy.data.images['Meera_BaseColor']; W, H = img.size
px = np.empty(W*H*4, np.float32); img.pixels.foreach_get(px); px = px.reshape(H, W, 4)
def sample(u, v):
    x = np.clip(u*W - 0.5, 0, W-1.001); y = np.clip(v*H - 0.5, 0, H-1.001)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); fx = (x-x0)[...,None]; fy = (y-y0)[...,None]
    return (px[y0,x0,:3]*(1-fx)+px[y0,x0+1,:3]*fx)*(1-fy) + (px[y0+1,x0,:3]*(1-fx)+px[y0+1,x0+1,:3]*fx)*fy
S = 512
out = np.zeros((S, 2*S, 4), np.float32); out[...,3] = 1
Lw = np.array([0.299,0.587,0.114])
SCLERA = np.array([0.935, 0.915, 0.895])
for k, side in enumerate(('L','R')):
    E = bpy.app.driver_namespace['eye_src_'+side]
    c, r, f, tri_co, tri_uv = E['c'], E['r'], E['f'], E['tri_co'], E['tri_uv']
    iris_deg = min(E['iris_deg'], 40.0)
    up = np.array([0,0,1.0]); e1 = np.cross(up, f); e1 /= np.linalg.norm(e1); e2 = np.cross(f, e1)
    bvh = BVHTree.FromPolygons([tuple(v) for v in tri_co.reshape(-1,3)], [(3*i,3*i+1,3*i+2) for i in range(len(tri_co))])
    jj, ii = np.meshgrid(np.arange(S), np.arange(S))
    du = (jj + 0.5)/S*2 - 1; dv = (ii + 0.5)/S*2 - 1
    rho = np.sqrt(du**2 + dv**2); beta = np.arctan2(dv, du); alpha = np.clip(rho, 0, 1)*np.pi
    adeg = np.degrees(alpha)
    dirs = np.cos(alpha)[...,None]*f + np.sin(alpha)[...,None]*(np.cos(beta)[...,None]*e1 + np.sin(beta)[...,None]*e2)
    col = np.zeros((S,S,3)); cov = np.zeros((S,S), bool)
    for a_, b_ in zip(*np.nonzero(adeg < iris_deg + 4)):
        p = c + r*dirs[a_, b_]
        loc, nrm, idx, dist = bvh.find_nearest(mathutils.Vector(p), 0.0012)
        if loc is None: continue
        t = tri_co[idx]; v0 = t[1]-t[0]; v1 = t[2]-t[0]; v2 = np.array(loc)-t[0]
        d00 = v0@v0; d01 = v0@v1; d11 = v1@v1; d20 = v2@v0; d21 = v2@v1; den = d00*d11 - d01*d01
        if abs(den) < 1e-18: continue
        w1 = (d11*d20 - d01*d21)/den; w2 = (d00*d21 - d01*d20)/den; w0 = 1-w1-w2
        uvp = tri_uv[idx][0]*w0 + tri_uv[idx][1]*w1 + tri_uv[idx][2]*w2
        col[a_, b_] = sample(np.array([uvp[0]]), np.array([uvp[1]]))[0]; cov[a_, b_] = True
    # radial profile from the lower 3/4 of the iris (upper part is shadowed by the lid in the paint)
    ring = np.round(rho*S/2).astype(int)
    rmax = int(np.ceil(iris_deg/180*S/2)) + 3
    lower = beta < np.radians(45)    # exclude the top-right lid intrusion sector
    lower &= beta > np.radians(-200)
    prof = np.zeros((rmax+1, 3)); L = col @ Lw
    for rr in range(rmax+1):
        m = cov & (ring == rr) & lower
        if m.sum() < 4: m = cov & (ring == rr)
        if m.sum() == 0: prof[rr] = prof[rr-1] if rr else np.array([0.05,0.04,0.03]); continue
        Lm = L[m]; keep = Lm < np.percentile(Lm, 85)   # drop highlights
        prof[rr] = np.median(col[m][keep] if keep.any() else col[m], 0)
    radial = prof[np.clip(ring, 0, rmax)]
    # keep original detail only where it agrees with the radial profile
    diff = np.linalg.norm(col - radial, axis=2)
    ok = cov & (diff < 0.10)
    okd = ok.copy()
    for _ in range(2):
        e = okd.copy(); e[1:] &= okd[:-1]; e[:-1] &= okd[1:]; e[:,1:] &= okd[:,:-1]; e[:,:-1] &= okd[:,1:]; okd = e
    iris_col = np.where(okd[...,None], 0.55*col + 0.45*radial, radial)
    vign = 1 - 0.14*np.clip((adeg - 45)/90, 0, 1)
    scl = SCLERA[None,None,:]*vign[...,None]
    blend = np.clip((adeg - (iris_deg - 1.5))/3.0, 0, 1)[...,None]
    out[:, k*S:(k+1)*S, :3] = iris_col*(1-blend) + scl*blend
eye = bpy.data.images['Meera_Eyes']
eye.pixels.foreach_set(out.ravel()); eye.update(); eye.save()
'''
r = submit("Eye texture v2", [{"label": "Rebuild irises from radial profile", "code": code_tex}])
result = {"job": r["job"]["state"]}

# ---- Delete painted bulges, add socket walls (region 6) (Meera call 59) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_backup = r'''
src = bpy.data.objects['Meera_Body']
bk = src.copy(); bk.data = src.data.copy(); bk.name = 'Meera_Body_PreSurgery'; bk.data.name = 'Meera_Body_PreSurgery'
coll = bpy.data.collections.get('Backup') or bpy.data.collections.new('Backup')
if coll.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(coll)
coll.objects.link(bk)
bpy.context.view_layer.layer_collection.children['Backup'].exclude = True
'''
code_cut = r'''
import bmesh, numpy as np, mathutils
ob = bpy.data.objects['Meera_Body']; me = ob.data
patch = bpy.app.driver_namespace['eye_patch_faces']
# darkest uniform texel (for socket walls)
small = bpy.data.images['review_basecolor_1024']; sw, sh = small.size
spx = np.empty(sw*sh*4, np.float32); small.pixels.foreach_get(spx); spx = spx.reshape(sh, sw, 4)
Ls = spx[...,:3] @ np.array([0.299,0.587,0.114])
from numpy.lib.stride_tricks import sliding_window_view
win = sliding_window_view(Ls, (9,9)).max(axis=(2,3))
iy, ix = np.unravel_index(np.argmin(win), win.shape)
dark_uv = ((ix + 4.5)/sw, (iy + 4.5)/sh)
bpy.app.driver_namespace['dark_uv'] = dark_uv
bm = bmesh.new(); bm.from_mesh(me)
bm.faces.ensure_lookup_table()
uvl = bm.loops.layers.uv.active
reg = bm.faces.layers.int.get('companion_region')
dele = [bm.faces[i] for i in np.nonzero(patch)[0]]
bmesh.ops.delete(bm, geom=dele, context='FACES')
walls = {}
for side in ('L','R'):
    c, r = bpy.app.driver_namespace['eye_fit_'+side]; c = mathutils.Vector(c)
    bnd = [e for e in bm.edges if e.is_boundary and (e.verts[0].co - c).length < r + 0.012]
    ret = bmesh.ops.extrude_edge_only(bm, edges=bnd)
    nverts = [g for g in ret['geom'] if isinstance(g, bmesh.types.BMVert)]
    nfaces = [g for g in ret['geom'] if isinstance(g, bmesh.types.BMFace)]
    for v in nverts:
        d = (c - v.co); d.normalize()
        v.co = v.co + d*0.0035
    for fa in nfaces:
        fa.material_index = 0
        if reg is not None: fa[reg] = 6
        fa.smooth = True
        for lp in fa.loops: lp[uvl].uv = dark_uv
    walls[side] = (len(bnd), len(nfaces))
bm.normal_update()
bm.to_mesh(me); bm.free(); me.update()
bpy.app.driver_namespace['eye_walls'] = walls
'''
r = submit("Eye sockets", [{"label": "Backup segmented mesh", "code": code_backup},
                           {"label": "Remove painted eye bulges and add socket walls", "code": code_cut}])
result = {"job": r["job"]["state"]}

# ---- Procedural matched irises + smooth lid rims (Meera call 63) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_iris = r'''
import numpy as np
eye = bpy.data.images['Meera_Eyes']; W, H = eye.size
px = np.empty(W*H*4, np.float32); eye.pixels.foreach_get(px); px = px.reshape(H, W, 4)
S = 512
jj, ii = np.meshgrid(np.arange(S), np.arange(S))
du = (jj + 0.5) - S/2; dv = (ii + 0.5) - S/2
rr = np.sqrt(du**2 + dv**2); beta = np.arctan2(dv, du)
Ri = 54.0; Rp = 0.47*Ri
t = rr / Ri
stops = [(0.0,(0.36,0.21,0.13)),(0.55,(0.42,0.26,0.16)),(0.72,(0.53,0.35,0.22)),(0.86,(0.42,0.27,0.17)),(0.95,(0.20,0.11,0.07)),(1.0,(0.15,0.08,0.05))]
ts = np.array([s[0] for s in stops]); cs = np.array([s[1] for s in stops])
iris = np.stack([np.interp(t, ts, cs[:,ch]) for ch in range(3)], -1)
rng = np.random.default_rng(11)
phase = rng.uniform(0, 2*np.pi, 5)
streak = (0.10*np.sin(beta*23 + phase[0] + 2.0*np.sin(beta*3 + phase[1])) + 0.06*np.sin(beta*41 + phase[2]) + 0.05*np.sin(beta*7 + phase[3]))
streak *= np.clip((t - 0.5)/0.2, 0, 1) * np.clip((0.97 - t)/0.1, 0, 1)
lower = 1 + 0.18*np.clip(-dv/Ri, 0, 1)*np.clip((t-0.5)/0.2,0,1)*np.clip((0.95-t)/0.15,0,1)
iris = iris * (1 + streak)[...,None] * lower[...,None]
pupil_w = np.clip((Rp + 1.5 - rr)/3.0, 0, 1)[...,None]
iris = iris*(1-pupil_w) + np.array([0.025,0.018,0.015])*pupil_w
for k in range(2):
    blk = px[:, k*S:(k+1)*S, :3]
    sclera = blk.copy()
    w = np.clip((Ri + 1.5 - rr)/3.0, 0, 1)[...,None]
    px[:, k*S:(k+1)*S, :3] = iris*w + sclera*(1-w)
# repaint sclera outside the iris uniformly (removes any leftover blended ring)
for k in range(2):
    blk = px[:, k*S:(k+1)*S, :3]
    alpha_deg = (rr/(S/2))*180
    vign = 1 - 0.14*np.clip((alpha_deg - 45)/90, 0, 1)
    scl = np.array([0.935,0.915,0.895])[None,None,:]*vign[...,None]
    w = np.clip((rr - Ri - 1.0)/2.0, 0, 1)[...,None]
    px[:, k*S:(k+1)*S, :3] = blk*(1-w) + scl*w
eye.pixels.foreach_set(px.ravel()); eye.update(); eye.save()
'''
code_rim = r'''
import bmesh, numpy as np, mathutils
ob = bpy.data.objects['Meera_Body']; me = ob.data
bm = bmesh.new(); bm.from_mesh(me)
reg = bm.faces.layers.int.get('companion_region')
for side in ('L','R'):
    c, r = bpy.app.driver_namespace['eye_fit_'+side]; c = mathutils.Vector(c)
    wall = [f for f in bm.faces if f[reg] == 6 and (f.calc_center_median() - c).length < r + 0.01]
    wall_verts = {v for f in wall for v in f.verts}
    # rim verts: wall verts that also touch a non-wall face
    rim = [v for v in wall_verts if any(f[reg] != 6 for f in v.link_faces)]
    inner = [v for v in wall_verts if v not in set(rim)]
    rimset = set(rim)
    nb = {v: [e.other_vert(v) for e in v.link_edges if e.other_vert(v) in rimset] for v in rim}
    for it in range(4):
        new = {}
        for v in rim:
            ns = nb[v]
            if len(ns) == 2:
                avg = (ns[0].co + ns[1].co) * 0.5
                p = v.co.lerp(avg, 0.5)
                d = p - c
                if d.length < r + 0.0003: p = c + d.normalized()*(r + 0.0003)
                new[v] = p
        for v, p in new.items(): v.co = p
    # each inner wall vert follows its rim neighbour
    for v in inner:
        rims = [e.other_vert(v) for e in v.link_edges if e.other_vert(v) in rimset]
        if rims:
            base = rims[0].co
            d = (c - base).normalized()
            v.co = base + d*0.0035
bm.normal_update(); bm.to_mesh(me); bm.free(); me.update()
'''
r = submit("Eye polish", [{"label": "Procedural matched irises", "code": code_iris},
                          {"label": "Smooth lid rims", "code": code_rim}])
result = {"job": r["job"]["state"]}
