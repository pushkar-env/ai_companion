# Verbatim from the Meera build (2026-10-08), live Blender 5.2.2 session via the Blender MCP.
# REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Meera_*'), coordinates (metres, Blender Z-up,
# character facing -Y, 1.60 m tall) and colour thresholds were measured for Meera. Re-measure
# each new character (probe calls + review renders) before reusing a block, and run each block
# as a staged live.submit job, not as a single execute call.
# Read SKILL.md first for the order, the checks and the failure notes for each stage.

# ---- Lip contact curve anchors + Dijkstra cut path (Meera call 70) ----
import bpy, numpy as np, heapq, bmesh, mathutils
ob = bpy.data.objects['Meera_Body']; me = ob.data
nv = len(me.vertices)
co = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
ed = np.empty(len(me.edges)*2, np.int32); me.edges.foreach_get('vertices', ed); ed = ed.reshape(-1,2)
ax = np.array([-0.0265,-0.0236,-0.0165,-0.0076,0.0012,0.0075,0.0119,0.0181,0.0235,0.0256])
az = np.array([1.367,1.3642,1.3635,1.3621,1.3618,1.3622,1.3638,1.3661,1.368,1.3718])
def zc(x): return np.interp(x, ax, az)
front = co[:,1] < -0.095
def corner(x, z):
    m = front & (np.abs(co[:,0]-x) < 0.0035) & (np.abs(co[:,2]-z) < 0.0035)
    idx = np.nonzero(m)[0]
    d = np.hypot(co[idx,0]-x, co[idx,2]-z) + 0.3*np.abs(co[idx,1] - co[idx,1].min())
    return int(idx[np.argmin(d)])
cR = corner(-0.0265, 1.367); cL = corner(0.0256, 1.3718)
band = front & (co[:,0] > -0.0285) & (co[:,0] < 0.0275) & (np.abs(co[:,2] - zc(co[:,0])) < 0.003)
band[cR] = band[cL] = True
adj = {}
for a, b in ed:
    if band[a] and band[b]:
        L = np.linalg.norm(co[a]-co[b]); mid = (co[a]+co[b])/2
        dz = abs(mid[2] - zc(mid[0]))
        w = L*(1 + 600*dz)
        adj.setdefault(a, []).append((b, w)); adj.setdefault(b, []).append((a, w))
dist = {cR: 0.0}; prev = {}; pq = [(0.0, cR)]
while pq:
    d, u = heapq.heappop(pq)
    if u == cL: break
    if d > dist.get(u, 1e9): continue
    for v, w in adj.get(u, []):
        nd = d + w
        if nd < dist.get(v, 1e9): dist[v] = nd; prev[v] = u; heapq.heappush(pq, (nd, v))
path = [cL]
while path[-1] != cR: path.append(prev[path[-1]])
path = path[::-1]
bpy.app.driver_namespace['lip_path'] = path
bpy.app.driver_namespace['lip_curve'] = (ax, az)
P = co[path]
# visualize path
old = bpy.data.objects.get('Review_LipPts')
if old: bpy.data.objects.remove(old, do_unlink=True)
bm = bmesh.new()
for p in P:
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.0005, matrix=mathutils.Matrix.Translation(mathutils.Vector(p) + mathutils.Vector((0,-0.001,0))))
m2 = bpy.data.meshes.new('Review_LipPts'); bm.to_mesh(m2); bm.free()
o2 = bpy.data.objects.new('Review_LipPts', m2); bpy.data.collections['Review'].objects.link(o2)
m2.materials.append(bpy.data.materials['ReviewRed'])
shoot = bpy.app.driver_namespace['companion_shoot']
sh = bpy.context.scene.display.shading; sh.color_type='TEXTURE'
shoot('lip_path','FRONT', center=(0.0,0,1.366), ortho=0.065, res=(1000,1000))
o2.hide_render = True
result = {"n": len(path), "cR": [round(float(v),4) for v in co[cR]], "cL": [round(float(v),4) for v in co[cL]], "maxdev_mm": round(float(np.abs(P[:,2]-zc(P[:,0])).max()*1000),2)}

# ---- Split lip seam, inner lip walls (region 7) + first mouth interior (Meera call 71) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_cut = r'''
import bmesh, numpy as np, mathutils
ob = bpy.data.objects['Meera_Body']; me = ob.data
path = bpy.app.driver_namespace['lip_path']
# dark lip texel: darkest texel among faces bordering the path (sampled from the 1024 review atlas)
small = bpy.data.images['review_basecolor_1024']; sw, sh = small.size
spx = np.empty(sw*sh*4, np.float32); small.pixels.foreach_get(spx); spx = spx.reshape(sh, sw, 4)
bm = bmesh.new(); bm.from_mesh(me)
bm.verts.ensure_lookup_table()
uvl = bm.loops.layers.uv.active
reg = bm.faces.layers.int.get('companion_region')
pv = [bm.verts[i] for i in path]
pedges = []
for a, b in zip(pv[:-1], pv[1:]):
    e = bm.edges.get((a, b))
    if e is None: raise RuntimeError('path edge missing')
    pedges.append(e)
best = None
for e in pedges:
    for f in e.link_faces:
        for lp in f.loops:
            u, v = lp[uvl].uv
            c = spx[min(int(v*sh), sh-1), min(int(u*sw), sw-1), :3]
            L = float(c @ np.array([0.299,0.587,0.114]))
            red = c[0] > c[1]*1.15
            if red and (best is None or L < best[0]): best = (L, (u, v))
lip_uv = best[1] if best else bpy.app.driver_namespace['dark_uv']
bpy.app.driver_namespace['lip_dark_uv'] = lip_uv
res = bmesh.ops.split_edges(bm, edges=pedges)
# classify boundary edges near the mouth into upper / lower chains
bnd = [e for e in bm.edges if e.is_boundary and abs(e.verts[0].co.x) < 0.03 and 1.355 < e.verts[0].co.z < 1.38 and e.verts[0].co.y < -0.09]
upper, lower = [], []
for e in bnd:
    f = e.link_faces[0]
    mid = (e.verts[0].co + e.verts[1].co)/2
    (upper if f.calc_center_median().z > mid.z else lower).append(e)
walls = {}
for name, chain, dz in (('upper', upper, 0.0009), ('lower', lower, -0.0009)):
    ret = bmesh.ops.extrude_edge_only(bm, edges=chain)
    nverts = [g for g in ret['geom'] if isinstance(g, bmesh.types.BMVert)]
    nfaces = [g for g in ret['geom'] if isinstance(g, bmesh.types.BMFace)]
    for v in nverts:
        v.co = v.co + mathutils.Vector((0, 0.0045, dz))
    for f in nfaces:
        f.material_index = 0; f.smooth = True
        if reg is not None: f[reg] = 7
        for lp in f.loops: lp[uvl].uv = lip_uv
    walls[name] = (len(chain), len(nfaces))
bm.normal_update(); bm.to_mesh(me); bm.free(); me.update()
bpy.app.driver_namespace['lip_walls'] = walls
'''
code_mouth = r'''
import bmesh, numpy as np, mathutils
from mathutils.bvhtree import BVHTree
body = bpy.data.objects['Meera_Body']
deps = bpy.context.evaluated_depsgraph_get()
bvh = BVHTree.FromObject(body, deps)
# texture blocks: teeth, gum, tongue, cavity
S = 64
tex = np.zeros((S, S, 4), np.float32); tex[...,3] = 1
cols = {'teeth': (0.94,0.92,0.86), 'gum': (0.62,0.27,0.29), 'tongue': (0.70,0.34,0.34), 'cavity': (0.20,0.045,0.055)}
blocks = {'teeth': (0,0), 'gum': (1,0), 'tongue': (0,1), 'cavity': (1,1)}
for k, (bx, by) in blocks.items():
    tex[by*32:(by+1)*32, bx*32:(bx+1)*32, :3] = cols[k]
img = bpy.data.images.get('Meera_Mouth') or bpy.data.images.new('Meera_Mouth', S, S, alpha=False)
img.pixels.foreach_set(tex.ravel()); img.update()
img.filepath_raw = 'D:/Blender/Companion_Character_Rig_20261008/export/textures/Meera_Mouth.png'; img.file_format = 'PNG'; img.save()
def block_uv(k):
    bx, by = blocks[k]; return ((bx*32+16)/S, (by*32+16)/S)
bm = bmesh.new()
uvl = bm.loops.layers.uv.new('UVMap')
part = bm.faces.layers.int.new('mouth_part')
def add_part(kind, build):
    before = set(bm.faces)
    build()
    new = [f for f in bm.faces if f not in before]
    for f in new:
        f[part] = list(blocks).index(kind)
        for lp in f.loops: lp[uvl].uv = block_uv(kind)
        f.smooth = True
    return new
# cavity ellipsoid, normals inward, clamped inside the head
center = mathutils.Vector((0.0, -0.090, 1.3605))
def cavity():
    m = mathutils.Matrix.Translation(center) @ mathutils.Matrix.Diagonal((0.026, 0.025, 0.017, 1))
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=1.0, matrix=m)
cav = add_part('cavity', cavity)
cv = {v for f in cav for v in f.verts}
for v in cv:
    d = v.co - center; L = d.length; dn = d.normalized()
    hit = bvh.ray_cast(center, dn, L + 0.01)
    if hit[0] is not None and (hit[0] - center).length < L + 0.0018:
        v.co = center + dn * max((hit[0] - center).length - 0.0018, 0.004)
bmesh.ops.reverse_faces(bm, faces=cav)
def teeth(z_top, z_bot, y0, half_w, k, thick):
    def build():
        n = 14
        verts = []
        for i in range(n+1):
            x = -half_w + 2*half_w*i/n
            y = y0 + k*x*x
            ring = []
            for (dy, z) in ((0, z_top), (0, z_bot), (thick, z_bot), (thick, z_top)):
                ring.append(bm.verts.new((x, y + dy, z)))
            verts.append(ring)
        for i in range(n):
            a, b = verts[i], verts[i+1]
            for j in range(4):
                bm.faces.new((a[j], b[j], b[(j+1)%4], a[(j+1)%4]))
        bm.faces.new(tuple(verts[0][::-1])); bm.faces.new(tuple(verts[-1]))
    return build
up_teeth = add_part('teeth', teeth(1.3715, 1.3627, -0.1112, 0.0175, 26.0, 0.0032))
lo_teeth = add_part('teeth', teeth(1.3606, 1.3525, -0.1085, 0.0160, 28.0, 0.0030))
def tongue():
    m = mathutils.Matrix.Translation((0.0, -0.093, 1.3545)) @ mathutils.Matrix.Diagonal((0.0155, 0.021, 0.0052, 1))
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1.0, matrix=m)
tg = add_part('tongue', tongue)
bmesh.ops.recalc_face_normals(bm, faces=up_teeth + lo_teeth + tg)
bm.normal_update()
me = bpy.data.meshes.new('Meera_Mouth'); bm.to_mesh(me); bm.free()
mo = bpy.data.objects.new('Meera_Mouth', me)
bpy.data.collections['Companion_Work'].objects.link(mo)
mat = bpy.data.materials.get('Meera_Mouth') or bpy.data.materials.new('Meera_Mouth')
mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes.get('Principled BSDF')
t = nt.nodes.new('ShaderNodeTexImage'); t.image = img; t.interpolation = 'Closest'
nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color']); bsdf.inputs['Roughness'].default_value = 0.45
me.materials.append(mat)
'''
r = submit("Mouth", [{"label": "Cut lip seam and add inner lip walls", "code": code_cut},
                     {"label": "Build mouth cavity, teeth and tongue", "code": code_mouth}])
result = {"job": r["job"]["state"]}

# ---- Harmonic jaw field + lip sets (Meera call 74) ----
import bpy, numpy as np
ob = bpy.data.objects['Meera_Body']; me = ob.data
nv = len(me.vertices); nf = len(me.polygons)
co = np.empty(nv*3, np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1,3)
ed = np.empty(len(me.edges)*2, np.int32); me.edges.foreach_get('vertices', ed); ed = ed.reshape(-1,2)
lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
fvs = np.empty(len(me.loops), np.int32); me.polygons.foreach_get('vertices', fvs)
reg = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', reg)
fol = np.repeat(np.arange(nf), lt)
x, y, z = co[:,0], co[:,1], co[:,2]
ax, az = bpy.app.driver_namespace['lip_curve']
zc = np.interp(np.clip(x, ax[0], ax[-1]), ax, az)
wallv = np.zeros(nv, bool); wallv[fvs[reg[fol] == 7]] = True
def flood(seed, allowed):
    reach = seed & allowed
    for it in range(400):
        ea = reach[ed[:,0]] & allowed[ed[:,1]] & ~reach[ed[:,1]]
        eb = reach[ed[:,1]] & allowed[ed[:,0]] & ~reach[ed[:,0]]
        if not (ea.any() or eb.any()): break
        reach[ed[ea,1]] = True; reach[ed[eb,0]] = True
    return reach
inner = (np.abs(x) < 0.0225) & (y < -0.085)
lower_seed = inner & (z < 1.350)
lower = flood(lower_seed, inner & (z < zc + 0.004))
upper_seed = inner & (z > 1.383)
upper = flood(upper_seed, inner & (z > zc - 0.004))
overlap = int((lower & upper).sum())
head = z > 1.27
fixed1 = (head & (z < 1.345) & (y < -0.07) & (np.abs(x) < 0.05)) | (lower & ~upper)
fixed0 = (head & (z > 1.398)) | (head & (y > -0.012) & (z > 1.33)) | (z < 1.312) | (upper & ~lower) | ~head
w = np.zeros(nv); w[fixed1] = 1.0
free = ~(fixed0 | fixed1)
a, b = ed[:,0], ed[:,1]
deg = np.zeros(nv); np.add.at(deg, a, 1); np.add.at(deg, b, 1)
for it in range(2000):
    s = np.zeros(nv); np.add.at(s, a, w[b]); np.add.at(s, b, w[a])
    w = np.where(free, s/np.maximum(deg, 1), w)
w = np.clip(w, 0, 1)
bpy.app.driver_namespace['jaw_w'] = w
bpy.app.driver_namespace['lip_sets'] = (lower & ~upper, upper & ~lower)
vcol = np.c_[w, 0.2*np.ones(nv), 1-w]
attr = me.color_attributes.get('debug_seg')
attr.data.foreach_set('color', np.c_[vcol[fvs], np.ones(len(fvs))].astype(np.float32).ravel())
sh = bpy.context.scene.display.shading; sh.color_type='VERTEX'
shoot = bpy.app.driver_namespace['companion_shoot']
shoot('jaw_w2_front','FRONT', center=(0,0,1.37), ortho=0.2, res=(800,800))
sh.color_type='TEXTURE'
result = {"lower": int(lower.sum()), "upper": int(upper.sum()), "overlap": overlap}

# ---- Mouth interior v2 (cavity, teeth, tongue) (Meera call 77) ----
import bpy
submit = bpy.app.driver_namespace['companion_submit']
code_mouth = r'''
import bmesh, numpy as np, mathutils
from mathutils.bvhtree import BVHTree
old = bpy.data.objects.get('Meera_Mouth')
if old:
    me_old = old.data; bpy.data.objects.remove(old, do_unlink=True); bpy.data.meshes.remove(me_old)
body = bpy.data.objects['Meera_Body']
# BVH of the body in its rest shape (ignore test shape keys)
bm0 = bmesh.new(); bm0.from_mesh(body.data); bvh = BVHTree.FromBMesh(bm0); bm0.free()
S = 64
tex = np.zeros((S, S, 4), np.float32); tex[...,3] = 1
cols = {'teeth': (0.95,0.93,0.88), 'gum': (0.66,0.30,0.32), 'tongue': (0.74,0.38,0.38), 'cavity': (0.30,0.075,0.085)}
blocks = {'teeth': (0,0), 'gum': (1,0), 'tongue': (0,1), 'cavity': (1,1)}
for k, (bx, by) in blocks.items():
    tex[by*32:(by+1)*32, bx*32:(bx+1)*32, :3] = cols[k]
img = bpy.data.images['Meera_Mouth']; img.pixels.foreach_set(tex.ravel()); img.update(); img.save()
def block_uv(k):
    bx, by = blocks[k]; return ((bx*32+16)/S, (by*32+16)/S)
bm = bmesh.new()
uvl = bm.loops.layers.uv.new('UVMap')
part = bm.faces.layers.int.new('mouth_part')
def add_part(kind, build):
    before = set(bm.faces); build()
    new = [f for f in bm.faces if f not in before]
    for f in new:
        f[part] = list(blocks).index(kind); f.smooth = True
        for lp in f.loops: lp[uvl].uv = block_uv(kind)
    return new
center = mathutils.Vector((0.0, -0.090, 1.3605))
def cavity():
    m = mathutils.Matrix.Translation(center) @ mathutils.Matrix.Diagonal((0.026, 0.025, 0.017, 1))
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=1.0, matrix=m)
cav = add_part('cavity', cavity)
for v in {v for f in cav for v in f.verts}:
    d = v.co - center; L = d.length; dn = d.normalized()
    hit = bvh.ray_cast(center, dn, L + 0.01)
    if hit[0] is not None and (hit[0] - center).length < L + 0.0018:
        v.co = center + dn * max((hit[0] - center).length - 0.0018, 0.004)
bmesh.ops.reverse_faces(bm, faces=cav)
def teeth(z_top, z_bot, y0, half_w, k, thick, n=14):
    def build():
        rings = []
        for i in range(n+1):
            x = -half_w + 2*half_w*i/n; y = y0 + k*x*x
            # rounded incisal edge: slightly shorter towards the sides
            zb = z_bot + (abs(x)/half_w)**2 * 0.0012 * (1 if z_bot < z_top else -1)
            rings.append([bm.verts.new((x, y+dy, z)) for (dy, z) in ((0, z_top), (0, zb), (thick, zb), (thick, z_top))])
        for i in range(n):
            a, b = rings[i], rings[i+1]
            for j in range(4): bm.faces.new((a[j], b[j], b[(j+1)%4], a[(j+1)%4]))
        bm.faces.new(tuple(rings[0][::-1])); bm.faces.new(tuple(rings[-1]))
    return build
up_teeth = add_part('teeth', teeth(1.3712, 1.3597, -0.1126, 0.0175, 25.0, 0.0034))
lo_teeth = add_part('teeth', teeth(1.3508, 1.3594, -0.1092, 0.0158, 28.0, 0.0030))
def tongue():
    m = mathutils.Matrix.Translation((0.0, -0.0885, 1.3555)) @ mathutils.Matrix.Diagonal((0.0150, 0.0195, 0.0062, 1))
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1.0, matrix=m)
tg = add_part('tongue', tongue)
bmesh.ops.recalc_face_normals(bm, faces=up_teeth + lo_teeth + tg)
bm.normal_update()
me = bpy.data.meshes.new('Meera_Mouth'); bm.to_mesh(me); bm.free()
mo = bpy.data.objects.new('Meera_Mouth', me)
bpy.data.collections['Companion_Work'].objects.link(mo)
me.materials.append(bpy.data.materials['Meera_Mouth'])
'''
r = submit("Mouth v2", [{"label": "Rebuild mouth interior", "code": code_mouth}])
result = {"job": r["job"]["state"]}

# ---- Mouth jaw weights (Meera call 79) ----
import bpy, numpy as np
pivot = np.array([0, -0.032, 1.405])
def rotx(P, ang):
    c, s = np.cos(ang), np.sin(ang); R = np.array([[1,0,0],[0,c,-s],[0,s,c]]); return (P - pivot) @ R.T + pivot
mo = bpy.data.objects['Meera_Mouth']; mm = mo.data
mv = len(mm.vertices)
mco = np.empty(mv*3, np.float64); mm.shape_keys.key_blocks['Basis'].data.foreach_get('co', mco); mco = mco.reshape(-1,3)
part = np.empty(len(mm.polygons), np.int32); mm.attributes['mouth_part'].data.foreach_get('value', part)
mlt = np.empty(len(mm.polygons), np.int32); mm.polygons.foreach_get('loop_total', mlt)
mfv = np.empty(len(mm.loops), np.int32); mm.polygons.foreach_get('vertices', mfv)
vpart = np.full(mv, -1); vpart[mfv] = np.repeat(part, mlt)
mw = np.zeros(mv)
mw[vpart == 2] = 1.0
mw[(vpart == 0) & (mco[:,2] <= 1.3596)] = 1.0
cav = vpart == 3; mw[cav] = np.clip((1.366 - mco[cav,2]) / 0.010, 0, 1)
bpy.app.driver_namespace['mouth_jaw_w'] = mw
bpy.app.driver_namespace['mouth_vpart'] = vpart
mm.shape_keys.key_blocks['TEST_jaw'].data.foreach_set('co', (mco + (rotx(mco, np.radians(14)) - mco)*mw[:,None]).ravel().astype(np.float32))
mm.update()
shoot = bpy.app.driver_namespace['companion_shoot']
shoot('lip_close_low2',(0.0,-3,-0.6), center=(0,-0.08,1.362), ortho=0.06, res=(900,900))
shoot('jaw_open_front2','FRONT', center=(0,0,1.37), ortho=0.16, res=(800,800))
result = {"ok":1}

# ---- Settle teeth/tongue, clear test keys (Meera call 80) ----
import bpy, numpy as np
mo = bpy.data.objects['Meera_Mouth']; mm = mo.data
vpart = bpy.app.driver_namespace['mouth_vpart']
# drop the temporary keys, edit the base mesh directly
mo.shape_key_clear()
mv = len(mm.vertices)
mco = np.empty(mv*3, np.float64); mm.vertices.foreach_get('co', mco); mco = mco.reshape(-1,3)
lowt = (vpart == 0) & (mco[:,2] <= 1.3596)
mco[lowt, 2] -= 0.0020
tg = vpart == 2
mco[tg, 2] += 0.0022
mco[tg, 1] += 0.0010
mm.vertices.foreach_set('co', mco.ravel().astype(np.float32)); mm.update()
mw = bpy.app.driver_namespace['mouth_jaw_w']
bpy.app.driver_namespace['mouth_lower_teeth'] = lowt
# remove the body's temporary test key too (real shapes are generated later)
body = bpy.data.objects['Meera_Body']
if body.data.shape_keys: body.shape_key_clear()
bpy.ops.wm.save_mainfile()
result = {"lower_teeth_verts": int(lowt.sum()), "tongue_verts": int(tg.sum())}
