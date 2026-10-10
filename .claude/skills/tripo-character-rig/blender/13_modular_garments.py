# Verbatim from the Arjun modular-wardrobe build (2026-10-10, ADR-075), live Blender 5.2.2 session via
# the Blender MCP. REFERENCE, NOT A ONE-SHOT SCRIPT: object names ('Arjun_*'), heights (metres, Blender
# Z-up, facing -Y, 1.78 m), cut lines (XM/XP), opening angles (DM/DP), button heights and texel paths were
# measured on Arjun. Re-measure every value for a new body (cross-section probes + review renders) and run
# each block as a staged live.submit job. Blocks share one namespace: 'S' maps step names to code strings
# kept in bpy.app.driver_namespace['outfit_steps']; a later block may reuse an earlier helper.
# Read SKILL.md stage 13 for the order, the checks and the pitfalls.

S = {}

# ==================================================================================================
# ---- 13a Split the fused Tripo mesh into a base body and garment objects (checkpoint first) ----
# Cut the trousers region at a plane, store every vertex normal as 'orig_normal', copy the body once per
# garment and delete the other regions, then restore the stored normals as custom normals so the seams
# shade exactly like the fused original. Face blendshapes stay on the base body only.
S['checkpoint'] = r'''
import os
os.makedirs('D:/Blender/Companion_Outfits_20261009_Arjun/checkpoints', exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath='D:/Blender/Companion_Outfits_20261009_Arjun/checkpoints/arjun-outfits-before-split.blend', copy=True)
'''

S['cut'] = r'''
import bmesh, numpy as np
ob = bpy.data.objects['Arjun_Body']; me = ob.data
bm = bmesh.new(); bm.from_mesh(me)
reg = bm.faces.layers.int.get('companion_region')
bot_faces = [f for f in bm.faces if f[reg]==3]
geom = list({v for f in bot_faces for v in f.verts}) + list({e for f in bot_faces for e in f.edges}) + bot_faces
res = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=(0,0,0.80), plane_no=(0,0,1))
bm.to_mesh(me); bm.free(); me.update()
n = len(me.vertices)
vn = np.empty(n*3, np.float32); me.vertex_normals.foreach_get('vector', vn)
a = me.attributes.get('orig_normal') or me.attributes.new('orig_normal', 'FLOAT_VECTOR', 'POINT')
a.data.foreach_set('vector', vn)
bpy.app.driver_namespace['split_info'] = {'cut_new_edges': len([g for g in res['geom_cut'] if isinstance(g, bmesh.types.BMEdge)])}
'''

S['parts'] = r'''
import bmesh, numpy as np
src = bpy.data.objects['Arjun_Body']
work = bpy.data.collections['Companion_Work']
rig = bpy.data.objects['Arjun_Rig']
def face_part(f, reg):
    r = f[reg]
    if r in (0,1,6,7,8): return 'base'
    if r in (2,9): return 'shirt'
    if r == 4: return 'shoes'
    if r == 3:
        zc = sum(v.co.z for v in f.verts)/len(f.verts)
        return 'lining' if zc > 0.80 else 'trousers'
    return 'base'
parts = {'Arjun_Shirt_Classic': ('shirt','lining'), 'Arjun_Trousers': ('trousers',), 'Arjun_Sneakers': ('shoes',)}
made = {}
for name, keep in parts.items():
    ob = bpy.data.objects.get(name)
    if ob is None:
        me = src.data.copy(); me.name = name
        ob = bpy.data.objects.new(name, me); work.objects.link(ob)
    ob.parent = rig; ob.matrix_parent_inverse.identity()
    if ob.data.shape_keys:
        ob.shape_key_clear()
    bm = bmesh.new(); bm.from_mesh(ob.data)
    reg = bm.faces.layers.int.get('companion_region')
    kill = [f for f in bm.faces if face_part(f, reg) not in keep]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    if 'lining' in keep:
        uvl = bm.loops.layers.uv.active
        top_idx = [i for i,s in enumerate(src.material_slots) if s.material and s.material.name=='Arjun_Top'][0]
        ref = [f for f in bm.faces if f[reg]==2 and sum(v.co.z for v in f.verts)/len(f.verts) < 0.95 and sum(v.co.y for v in f.verts)/len(f.verts) > 0.1]
        u = sum(l[uvl].uv.x for f in ref for l in f.loops)/sum(len(f.loops) for f in ref)
        v_ = sum(l[uvl].uv.y for f in ref for l in f.loops)/sum(len(f.loops) for f in ref)
        for f in bm.faces:
            if face_part(f, reg) == 'lining':
                f.material_index = top_idx; f[reg] = 10
                for l in f.loops: l[uvl].uv = (u, v_)
    bm.to_mesh(ob.data); bm.free()
    made[name] = len(ob.data.vertices)
bm = bmesh.new(); bm.from_mesh(src.data)
reg = bm.faces.layers.int.get('companion_region')
kill = [f for f in bm.faces if face_part(f, reg) != 'base']
bmesh.ops.delete(bm, geom=kill, context='FACES')
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(src.data); bm.free(); src.data.update()
made['Arjun_Body'] = len(src.data.vertices)
bpy.app.driver_namespace['split_made'] = made
'''

S['slots'] = r'''
import numpy as np
for name in ('Arjun_Body','Arjun_Shirt_Classic','Arjun_Trousers','Arjun_Sneakers'):
    ob = bpy.data.objects[name]; me = ob.data
    used = set(p.material_index for p in me.polygons)
    for i in reversed(range(len(ob.material_slots))):
        if i not in used:
            idx = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', idx)
            idx[idx > i] -= 1
            me.materials.pop(index=i)
            me.polygons.foreach_set('material_index', idx)
            used = set(int(k) for k in idx)
    n = len(me.vertices)
    vn = np.empty(n*3, np.float32); me.attributes['orig_normal'].data.foreach_get('vector', vn)
    vn = vn.reshape(-1,3); vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-9)[:,None]
    for p in me.polygons: p.use_smooth = True
    me.normals_split_custom_set_from_vertices([tuple(v) for v in vn])
    mod = next((m for m in ob.modifiers if m.type=='ARMATURE'), None)
    if mod is None:
        mod = ob.modifiers.new('Armature', 'ARMATURE')
    mod.object = bpy.data.objects['Arjun_Rig']
    me.update()
'''

S['save'] = r'''
bpy.ops.wm.save_mainfile()
'''

# ==================================================================================================
# ---- 13b Fix pieces the colour segmentation put on the wrong side ----
# Arjun: a collar strip and shoulder flecks were labelled skin (luminance 60-100), the fingernails were
# labelled shirt. Render each part alone before going on; every leftover shows once the parts separate.
S['move_select'] = r'''
import numpy as np, bmesh
A = bpy.app.driver_namespace['atlas_u8']; H,W,_ = A.shape
ob = bpy.data.objects['Arjun_Body']; me = ob.data
nl = len(me.loops); uvs = np.empty(nl*2, np.float32); me.uv_layers.active.data.foreach_get('uv', uvs); uvs = uvs.reshape(-1,2)
nf = len(me.polygons)
ls = np.empty(nf, np.int32); me.polygons.foreach_get('loop_start', ls)
lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
cen = np.empty(nf*3, np.float32); me.polygons.foreach_get('center', cen); cen = cen.reshape(-1,3)
fu = np.add.reduceat(uvs[:,0], ls)/lt; fv = np.add.reduceat(uvs[:,1], ls)/lt
col = A[np.clip((fv*H).astype(int),0,H-1), np.clip((fu*W).astype(int),0,W-1)].astype(float)
L = col.mean(1)
reg = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', reg)
zone = (cen[:,2] < 1.52) & (np.abs(cen[:,0]) < 0.45) & (reg == 0)
move = zone & (L < 100)
# skin-coloured highlight islands left on the shirt: small components of remaining zone faces
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
keep_zone = set(np.nonzero(zone & ~move)[0].tolist())
seen = set(); extra = []
for fi in keep_zone:
    if fi in seen: continue
    comp = [fi]; seen.add(fi); stack = [fi]
    while stack:
        g = bm.faces[stack.pop()]
        for e in g.edges:
            for h in e.link_faces:
                if h.index in keep_zone and h.index not in seen:
                    seen.add(h.index); comp.append(h.index); stack.append(h.index)
    if len(comp) < 60: extra.extend(comp)
bm.free()
move[np.array(extra, dtype=int)] = True
bpy.app.driver_namespace['move_faces'] = np.nonzero(move)[0]
bpy.app.driver_namespace['move_stats'] = {'dark': int((zone & (L<100)).sum()), 'islands': len(extra), 'total': int(move.sum())}
'''

S['move_apply'] = r'''
import numpy as np, bmesh
base = bpy.data.objects['Arjun_Body']; shirt = bpy.data.objects['Arjun_Shirt_Classic']
mv = set(bpy.app.driver_namespace['move_faces'].tolist())
top_mat = bpy.data.materials['Arjun_Top']
tmp = base.copy(); tmp.data = base.data.copy(); tmp.name = 'tmp_move'; bpy.data.collections['Companion_Work'].objects.link(tmp)
if tmp.data.shape_keys: tmp.shape_key_clear()
bm = bmesh.new(); bm.from_mesh(tmp.data); bm.faces.ensure_lookup_table()
reg = bm.faces.layers.int.get('companion_region')
kill = [f for f in bm.faces if f.index not in mv]
for f in bm.faces:
    if f.index in mv: f[reg] = 2; f.material_index = 0
bmesh.ops.delete(bm, geom=kill, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(tmp.data); bm.free()
tmp.data.materials.clear(); tmp.data.materials.append(top_mat)
for o in bpy.context.view_layer.objects: o.select_set(False)
shirt.select_set(True); tmp.select_set(True)
with bpy.context.temp_override(active_object=shirt, object=shirt, selected_objects=[shirt,tmp], selected_editable_objects=[shirt,tmp]):
    bpy.ops.object.join()
# weld the seam between the moved faces and the shirt
bm = bmesh.new(); bm.from_mesh(shirt.data)
bnd = [v for v in bm.verts if v.is_boundary]
bmesh.ops.remove_doubles(bm, verts=bnd, dist=1e-6)
bm.to_mesh(shirt.data); bm.free()
# remove from base
bm = bmesh.new(); bm.from_mesh(base.data); bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[bm.faces[i] for i in sorted(mv)], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(base.data); bm.free()
for o in (base, shirt):
    me = o.data
    vn = np.empty(len(me.vertices)*3, np.float32); me.attributes['orig_normal'].data.foreach_get('vector', vn)
    vn = vn.reshape(-1,3); vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-9)[:,None]
    for p in me.polygons: p.use_smooth = True
    me.normals_split_custom_set_from_vertices([tuple(v) for v in vn])
    me.update()
bpy.app.driver_namespace['move_result'] = {'base_v': len(base.data.vertices), 'shirt_v': len(shirt.data.vertices), 'shirt_mats': [m.name for m in shirt.data.materials]}
'''

S['nails'] = r'''
import bmesh, numpy as np
base = bpy.data.objects['Arjun_Body']; shirt = bpy.data.objects['Arjun_Shirt_Classic']; ch = bpy.data.objects['Arjun_Shirt_Chambray']
def hand_faces(ob):
    me = ob.data
    cen = np.empty(len(me.polygons)*3, np.float32); me.polygons.foreach_get('center', cen); cen = cen.reshape(-1,3)
    return set(np.nonzero(np.abs(cen[:,0]) > 0.58)[0].tolist())
mv = hand_faces(shirt)
skin_mat = bpy.data.materials['Arjun_Skin']
tmp = shirt.copy(); tmp.data = shirt.data.copy(); tmp.name = 'tmp_nails'; bpy.data.collections['Companion_Work'].objects.link(tmp)
bm = bmesh.new(); bm.from_mesh(tmp.data); bm.faces.ensure_lookup_table()
rl = bm.faces.layers.int.get('companion_region')
for f in bm.faces:
    if f.index in mv: f[rl] = 0; f.material_index = 0
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in mv], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(tmp.data); bm.free()
tmp.data.materials.clear(); tmp.data.materials.append(skin_mat)
# the base body has shape keys: give the temp mesh matching (zero-delta) keys so the join keeps them
if base.data.shape_keys:
    tmp.shape_key_add(name='Basis')
for o in bpy.context.view_layer.objects: o.select_set(False)
base.select_set(True); tmp.select_set(True)
with bpy.context.temp_override(active_object=base, object=base, selected_objects=[base,tmp], selected_editable_objects=[base,tmp]):
    bpy.ops.object.join()
bm = bmesh.new(); bm.from_mesh(base.data)
bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if v.is_boundary], dist=1e-6)
bm.to_mesh(base.data); bm.free()
for o, kill in ((shirt, mv), (ch, hand_faces(ch))):
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in sorted(kill)], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free(); o.data.update()
for o in (base, shirt):
    m = o.data
    vn = np.empty(len(m.vertices)*3, np.float32); m.attributes['orig_normal'].data.foreach_get('vector', vn)
    vn = vn.reshape(-1,3); vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-9)[:,None]
    for p in m.polygons: p.use_smooth = True
    m.normals_split_custom_set_from_vertices([tuple(v) for v in vn]); m.update()
bpy.app.driver_namespace['nails_info'] = {'moved': len(mv), 'base_v': len(base.data.vertices), 'base_shapes': len(base.data.shape_keys.key_blocks) if base.data.shape_keys else 0}
'''

# ==================================================================================================
# ---- 13c Trousers own the visible hip band; rebuild a real waist and waistband ----
# The band just below the shirt hem is the visible top of the trousers, so it moves to the trousers with its
# original texture coordinates restored face by face (per-loop projection smears across chart seams). Specks
# of shirt texture inside it become trousers too. Re-cut at a clean plane below the tangled front-hem
# geometry and loft rings up to a 4 cm waistband with a folded top edge (the tee tucks inside it).
S['restore_lining_uv2'] = r'''
import numpy as np, bmesh, mathutils
from mathutils.bvhtree import BVHTree
src_path = 'D:/Blender/Companion_Outfits_20261009_Arjun/checkpoints/arjun-outfits-before-split.blend'
with bpy.data.libraries.load(src_path, link=False) as (df, dt):
    dt.meshes = ['Arjun_Body']
orig = dt.meshes[0]; orig.name = 'orig_body_ref'
reg = np.empty(len(orig.polygons), np.int32); orig.attributes['companion_region'].data.foreach_get('value', reg)
uvd = orig.uv_layers.active.data
ofaces = [p for p in orig.polygons if reg[p.index] == 3]
key = lambda c: (round(c[0],5), round(c[1],5), round(c[2],5))
exact = {}
for p in ofaces:
    exact[key(p.center)] = p
bvh = BVHTree.FromPolygons([v.co.copy() for v in orig.vertices], [tuple(p.vertices) for p in ofaces])
ob = bpy.data.objects['Arjun_Trousers']
bm = bmesh.new(); bm.from_mesh(ob.data)
uvl = bm.loops.layers.uv.active
n_exact = n_affine = 0
for f in bm.faces:
    c = f.calc_center_median()
    if c.z <= 0.795: continue
    p = exact.get(key(c))
    if p is not None and len(p.vertices) == len(f.verts):
        vmap = {}
        for li in p.loop_indices:
            vmap[key(orig.vertices[orig.loops[li].vertex_index].co)] = uvd[li].uv.copy()
        ok = all(key(l.vert.co) in vmap for l in f.loops)
        if ok:
            for l in f.loops: l[uvl].uv = vmap[key(l.vert.co)]
            n_exact += 1; continue
    loc, nrm, idx, d = bvh.find_nearest(c)
    p = ofaces[idx]
    li = list(p.loop_indices)[:3]
    P = [orig.vertices[orig.loops[i].vertex_index].co for i in li]
    U = [uvd[i].uv for i in li]
    e1 = P[1]-P[0]; e2 = P[2]-P[0]; nn = e1.cross(e2)
    M = mathutils.Matrix((e1, e2, nn)).transposed()
    try:
        Mi = M.inverted()
    except ValueError:
        continue
    for l in f.loops:
        q = l.vert.co - P[0]
        q = q - nn*(q.dot(nn)/max(nn.dot(nn),1e-20))
        a, b, _ = Mi @ q
        l[uvl].uv = U[0] + (U[1]-U[0])*a + (U[2]-U[0])*b
    n_affine += 1
bm.to_mesh(ob.data); bm.free()
bpy.data.meshes.remove(orig)
bpy.app.driver_namespace['lining_uv2'] = {'exact': n_exact, 'affine': n_affine}
'''

S['band_to_trousers'] = r'''
import numpy as np, bmesh
A = bpy.app.driver_namespace['atlas_u8']; H,W,_ = A.shape
shirt = bpy.data.objects['Arjun_Shirt_Classic']; trou = bpy.data.objects['Arjun_Trousers']
me = shirt.data
nl = len(me.loops); uvs = np.empty(nl*2, np.float32); me.uv_layers.active.data.foreach_get('uv', uvs); uvs = uvs.reshape(-1,2)
nf = len(me.polygons)
ls = np.empty(nf, np.int32); me.polygons.foreach_get('loop_start', ls)
lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
cen = np.empty(nf*3, np.float32); me.polygons.foreach_get('center', cen); cen = cen.reshape(-1,3)
reg = np.empty(nf, np.int32); me.attributes['companion_region'].data.foreach_get('value', reg)
fu = np.add.reduceat(uvs[:,0], ls)/lt; fv = np.add.reduceat(uvs[:,1], ls)/lt
L = A[np.clip((fv*H).astype(int),0,H-1), np.clip((fu*W).astype(int),0,W-1)].astype(float).mean(1)
move = ((reg==2) & (cen[:,2] < 0.90) & (L > 90)) | (reg==10)
mv = set(np.nonzero(move)[0].tolist())
bot_mat = bpy.data.materials['Arjun_Bottom']
tmp = shirt.copy(); tmp.data = shirt.data.copy(); tmp.name = 'tmp_band'; bpy.data.collections['Companion_Work'].objects.link(tmp)
bm = bmesh.new(); bm.from_mesh(tmp.data); bm.faces.ensure_lookup_table()
rl = bm.faces.layers.int.get('companion_region')
for f in bm.faces:
    if f.index in mv: f[rl] = 3; f.material_index = 0
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in mv], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(tmp.data); bm.free()
tmp.data.materials.clear(); tmp.data.materials.append(bot_mat)
for o in bpy.context.view_layer.objects: o.select_set(False)
trou.select_set(True); tmp.select_set(True)
with bpy.context.temp_override(active_object=trou, object=trou, selected_objects=[trou,tmp], selected_editable_objects=[trou,tmp]):
    bpy.ops.object.join()
bm = bmesh.new(); bm.from_mesh(trou.data)
bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if v.is_boundary], dist=1e-6)
bm.to_mesh(trou.data); bm.free()
bm = bmesh.new(); bm.from_mesh(shirt.data); bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[bm.faces[i] for i in sorted(mv)], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(shirt.data); bm.free()
for o in (shirt, trou):
    m = o.data
    vn = np.empty(len(m.vertices)*3, np.float32); m.attributes['orig_normal'].data.foreach_get('vector', vn)
    vn = vn.reshape(-1,3); vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-9)[:,None]
    for p in m.polygons: p.use_smooth = True
    m.normals_split_custom_set_from_vertices([tuple(v) for v in vn])
    if 'dbg' in m.color_attributes: m.color_attributes.remove(m.color_attributes['dbg'])
    m.update()
bpy.app.driver_namespace['band_result'] = {'moved': len(mv), 'shirt_v': len(shirt.data.vertices), 'trousers_v': len(trou.data.vertices), 'trousers_mats': [m.name for m in trou.data.materials], 'trousers_boundary_loops': None}
'''

S['islands'] = r'''
import bmesh, numpy as np
def islands(ob, zmax=0.915, maxf=300):
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
    seen = set(); out = []
    for f in bm.faces:
        if f.index in seen: continue
        comp = [f.index]; seen.add(f.index); st = [f]
        while st:
            g = st.pop()
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: seen.add(h.index); comp.append(h.index); st.append(h)
        if len(comp) < maxf and max(max(v.co.z for v in bm.faces[i].verts) for i in comp) < zmax:
            out.extend(comp)
    bm.free(); return set(out)
shirt = bpy.data.objects['Arjun_Shirt_Classic']; trou = bpy.data.objects['Arjun_Trousers']; ch = bpy.data.objects['Arjun_Shirt_Chambray']
mv = islands(shirt)
bot_mat = bpy.data.materials['Arjun_Bottom']
tmp = shirt.copy(); tmp.data = shirt.data.copy(); tmp.name = 'tmp_isl'; bpy.data.collections['Companion_Work'].objects.link(tmp)
bm = bmesh.new(); bm.from_mesh(tmp.data); bm.faces.ensure_lookup_table()
rl = bm.faces.layers.int.get('companion_region')
for f in bm.faces:
    if f.index in mv: f[rl] = 3; f.material_index = 0
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in mv], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(tmp.data); bm.free()
tmp.data.materials.clear(); tmp.data.materials.append(bot_mat)
for o in bpy.context.view_layer.objects: o.select_set(False)
trou.select_set(True); tmp.select_set(True)
with bpy.context.temp_override(active_object=trou, object=trou, selected_objects=[trou,tmp], selected_editable_objects=[trou,tmp]):
    bpy.ops.object.join()
bm = bmesh.new(); bm.from_mesh(trou.data)
bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if v.is_boundary], dist=1e-6)
bm.to_mesh(trou.data); bm.free()
for o, kill in ((shirt, mv), (ch, islands(ch))):
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in sorted(kill)], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free(); o.data.update()
bpy.app.driver_namespace['islands_info'] = {'moved': len(mv)}
'''

S['trousers_cut2'] = r'''
import bmesh
trou = bpy.data.objects['Arjun_Trousers']
bm = bmesh.new(); bm.from_mesh(trou.data)
geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=(0,0,0.835), plane_no=(0,0,1), clear_outer=True)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bedges = [e for e in bm.edges if e.is_boundary and min(v.co.z for v in e.verts) > 0.80]
adj = {}
for e in bedges:
    a, b = e.verts; adj.setdefault(a, set()).add(b); adj.setdefault(b, set()).add(a)
seen = set(); comps = []
for v in adj:
    if v in seen: continue
    st = [v]; seen.add(v); c = 0
    while st:
        u = st.pop(); c += 1
        for w in adj[u]:
            if w not in seen: seen.add(w); st.append(w)
    comps.append(c)
bm.to_mesh(trou.data); bm.free(); trou.data.update()
bpy.app.driver_namespace['trousers_cut2'] = {'loops': sorted(comps, reverse=True)}
'''

S['waist_ext3'] = r'''
import numpy as np, bmesh, mathutils
BZ = [0.95, 1.00, 1.10, 1.20, 1.30]
BA = [0.150, 0.150, 0.153, 0.160, 0.168]
BF = [0.106, 0.108, 0.114, 0.120, 0.122]
BB = [0.122, 0.122, 0.123, 0.127, 0.132]
def r_body(t, z):
    a = np.interp(z, BZ, BA); b = np.interp(z, BZ, BF) if abs(t) < np.pi/2 else np.interp(z, BZ, BB)
    return 1.0/np.sqrt((np.sin(t)/a)**2 + (np.cos(t)/b)**2)
ob = bpy.data.objects['Arjun_Trousers']
bm = bmesh.new(); bm.from_mesh(ob.data); bm.verts.ensure_lookup_table()
bedges = [e for e in bm.edges if e.is_boundary and min(v.co.z for v in e.verts) > 0.80]
adj = {}
for e in bedges:
    a, b = e.verts; adj.setdefault(a, []).append(b); adj.setdefault(b, []).append(a)
loops = []; used = set()
for s in adj:
    if s in used: continue
    loop = [s]; used.add(s); prev = None; cur = s
    while True:
        nxt = [w for w in adj[cur] if w is not prev and (w not in used or w is s)]
        if not nxt: break
        w = nxt[0]
        if w is s: break
        loop.append(w); used.add(w); prev, cur = cur, w
    loops.append(loop)
loops.sort(key=len, reverse=True)
loop = loops[0]
TH = np.array([np.arctan2(v.co.x, -v.co.y) for v in loop])
if np.sum(np.diff(np.unwrap(TH))) < 0: loop.reverse(); TH = TH[::-1]
RB = np.array([np.hypot(v.co.x, v.co.y) for v in loop])
uvl = bm.loops.layers.uv.active; rl = bm.faces.layers.int.get('companion_region')
REC = 0.0035
profile = [(0.850, 'keep', 0), (0.866, 'rec', 0), (0.880, 'rec', 0), (0.893, 'rec', 0), (0.910, 'blend', 0), (0.928, 'blend', 0), (0.945, 'blend', 0), (0.959, 'body', 0.0042),
           (0.961, 'body', 0.0052), (0.979, 'body', 0.0054), (0.997, 'body', 0.0052),
           (0.9995, 'body', 0.0038), (0.9985, 'body', 0.0024), (0.990, 'body', 0.0018), (0.978, 'body', 0.0016)]
rings = [loop]
for (z, mode, off) in profile:
    ring = []
    for j, v in enumerate(loop):
        t = TH[j]
        if mode == 'keep':
            r = RB[j]
        elif mode == 'rec':
            r = RB[j] - REC
        elif mode == 'blend':
            s = (z - 0.893)/(0.959 - 0.893); s = s*s*(3 - 2*s)
            r = (RB[j] - REC)*(1 - s) + (r_body(t, 0.959) + 0.0042)*s
        else:
            r = r_body(t, z) + off
        ring.append(bm.verts.new((r*np.sin(t), -r*np.cos(t), z)))
    rings.append(ring)
n = len(loop); newf = 0
mat = loop[0].link_faces[0].material_index
for k in range(len(rings)-1):
    A, B = rings[k], rings[k+1]
    for j in range(n):
        j2 = (j+1) % n
        f = bm.faces.new((A[j], A[j2], B[j2], B[j]))
        f.material_index = mat; f[rl] = 11 if k < 8 else 12
        for l in f.loops: l[uvl].uv = (0.0, 0.0)
        newf += 1
new_faces = [f for f in bm.faces if f[rl] in (11,12)]
# make the new faces face outward (away from the torso axis) except the inner facing rings
for f in new_faces:
    c = f.calc_center_median(); radial = mathutils.Vector((c.x, c.y, 0)).normalized()
    inner = c.z > 0.975 and f.normal.z < -0.5
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(ob.data); bm.free(); ob.data.update()
for p in ob.data.polygons: p.use_smooth = True
bpy.app.driver_namespace['waist_info'] = {'loops': [len(l) for l in loops][:5], 'loop': n, 'new_faces': newf}
'''

# ==================================================================================================
# ---- 13d Open shirt from the closed one ----
# Remove the folded multi-layer button strip instead of cutting through it, open both panels along a
# smoothed shirt-shell grid (median + blur removes buttons/pocket bumps, so every vertex moves; per-vertex
# rays left spikes), straighten the hem to its lower envelope, fold the open edges with radial normals,
# then clean interior flaps, old button remnants, the neckband strap across the V and needle faces.
S['chambray_copy'] = r'''
import bmesh, numpy as np
src = bpy.data.objects['Arjun_Shirt_Classic']
ob = bpy.data.objects.get('Arjun_Shirt_Chambray')
if ob is None:
    me = src.data.copy(); me.name = 'Arjun_Shirt_Chambray'
    ob = src.copy(); ob.data = me; ob.name = 'Arjun_Shirt_Chambray'
    bpy.data.collections['Companion_Work'].objects.link(ob)
ob.hide_render = False
# remove the white undershirt bits at the front hem (keep the real buttons)
bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
rl = bm.faces.layers.int.get('companion_region')
seen = set(); kill = []
for f in bm.faces:
    if f[rl] != 9 or f.index in seen: continue
    comp = [f]; seen.add(f.index); st = [f]
    while st:
        g = st.pop()
        for e in g.edges:
            for h in e.link_faces:
                if h[rl] == 9 and h.index not in seen:
                    seen.add(h.index); comp.append(h); st.append(h)
    c = sum((g.calc_center_median() for g in comp), __import__('mathutils').Vector())/len(comp)
    if c.z < 0.93 and (len(comp) > 35 or c.y > -0.13):
        kill.extend(comp)
bmesh.ops.delete(bm, geom=kill, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(ob.data); bm.free(); ob.data.update()
bpy.app.driver_namespace['chambray_info'] = {'removed_white': len(kill), 'v': len(ob.data.vertices)}
'''

S['chambray_strip'] = r'''
import bmesh, numpy as np
ob = bpy.data.objects['Arjun_Shirt_Chambray']
XM, XP = -0.026, -0.002
bm = bmesh.new(); bm.from_mesh(ob.data)
def front(f):
    c = f.calc_center_median(); return c.y < -0.04 and 0.79 < c.z < 1.347 and XM - 0.03 < c.x < XP + 0.03
for X in (XM, XP):
    faces = [f for f in bm.faces if front(f)]
    geom = list({v for f in faces for v in f.verts}) + list({e for f in faces for e in f.edges}) + faces
    bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=(X,0,0), plane_no=(1,0,0))
kill = [f for f in bm.faces if (lambda c: c.y < -0.04 and 0.79 < c.z < 1.347 and XM + 1e-5 < c.x < XP - 1e-5)(f.calc_center_median())]
bmesh.ops.delete(bm, geom=kill, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
# remove now-floating fragments (old buttons and placket slivers)
bm.faces.ensure_lookup_table(); seen = set(); small = []
for f in bm.faces:
    if f.index in seen: continue
    comp = [f]; seen.add(f.index); st = [f]
    while st:
        g = st.pop()
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen: seen.add(h.index); comp.append(h); st.append(h)
    if len(comp) < 400: small.extend(comp)
bmesh.ops.delete(bm, geom=small, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(ob.data); bm.free(); ob.data.update()
bpy.app.driver_namespace['chambray_strip'] = {'removed': len(kill), 'fragments': len(small), 'v': len(ob.data.vertices)}
'''

S['chambray_grid'] = r'''
import numpy as np, mathutils
from mathutils.bvhtree import BVHTree
YC = 0.0
src = bpy.data.objects['Arjun_Shirt_Classic']; sm = src.data
shell = BVHTree.FromPolygons([v.co.copy() for v in sm.vertices], [tuple(p.vertices) for p in sm.polygons])
TH = np.radians(np.arange(-150, 150.5, 1.0)); ZZ = np.arange(0.78, 1.50, 0.004)
R = np.full((len(ZZ), len(TH)), np.nan)
for i, z in enumerate(ZZ):
    o = mathutils.Vector((0.0, YC, z))
    for j, t in enumerate(TH):
        d = mathutils.Vector((np.sin(t), -np.cos(t), 0.0))
        hit = shell.ray_cast(o + d*0.45, -d, 0.45)
        if hit[0] is not None: R[i, j] = (hit[0] - o).length
# fill holes along theta then z with nearest valid values
def fill(a):
    a = a.copy()
    for i in range(a.shape[0]):
        row = a[i]; m = np.isfinite(row)
        if m.sum() >= 2: a[i] = np.interp(np.arange(len(row)), np.nonzero(m)[0], row[m])
    for j in range(a.shape[1]):
        col = a[:, j]; m = np.isfinite(col)
        if m.sum() >= 2: a[:, j] = np.interp(np.arange(len(col)), np.nonzero(m)[0], col[m])
    return a
R = fill(R)
# remove protrusions (buttons, pocket edge): 5x5 median, then separable gaussian blur
from numpy.lib.stride_tricks import sliding_window_view
P = np.pad(R, 2, mode='edge'); R = np.median(sliding_window_view(P, (5,5)), axis=(-1,-2))
def blur(a, sz, st):
    def k(s):
        n = int(3*s)+1; x = np.arange(-n, n+1); g = np.exp(-x*x/(2*s*s)); return g/g.sum()
    kz, kt = k(sz), k(st)
    a = np.apply_along_axis(lambda c: np.convolve(np.pad(c, len(kz)//2, mode='edge'), kz, 'valid'), 0, a)
    a = np.apply_along_axis(lambda r: np.convolve(np.pad(r, len(kt)//2, mode='edge'), kt, 'valid'), 1, a)
    return a
R = blur(R, 2.0, 2.5)
bpy.app.driver_namespace['shell_grid'] = (TH, ZZ, R)
'''

S['chambray_open4'] = r'''
import bmesh, numpy as np, mathutils
XM, XP = -0.026, -0.002; YC = 0.0
TH, ZZ, R = bpy.app.driver_namespace['shell_grid']
def Rs(t, z):
    fi = np.clip((z - ZZ[0])/(ZZ[1]-ZZ[0]), 0, len(ZZ)-1.001); fj = np.clip((t - TH[0])/(TH[1]-TH[0]), 0, len(TH)-1.001)
    i0 = int(fi); j0 = int(fj); a = fi - i0; b = fj - j0
    return (R[i0,j0]*(1-a)*(1-b) + R[i0+1,j0]*a*(1-b) + R[i0,j0+1]*(1-a)*b + R[i0+1,j0+1]*a*b)
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]
DM = [9, 9, 11, 8, 6, 2, 0]
DP = [23, 23, 23, 21, 15, 5, 0]
ob = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ob.data); bm.verts.ensure_lookup_table()
new = {}; maxd = 0.0

for v in bm.verts:
    p = v.co
    if p.y > 0.06 or p.z < 0.78 or p.z > 1.47: continue
    fc = [f.calc_center_median().x for f in v.link_faces]
    side = 1 if (sum(fc)/len(fc)) > (XM + XP)/2 else -1
    th_cut = np.arctan2(XP if side > 0 else XM, 0.15)
    r_v = np.hypot(p.x, p.y - YC); th = np.arctan2(p.x, -(p.y - YC))
    phi = np.degrees(side*(th - th_cut))
    if phi < -3: continue
    phi1 = float(np.interp(p.z, [0.78, 1.15, 1.30, 1.47], [140, 140, 95, 80]))
    t = np.clip((phi - 8.0)/(phi1 - 8.0), 0, 1); w = 1 - (0.7*t + 0.3*t*t*(3 - 2*t))
    if w <= 0: continue
    dth = np.radians(side*float(np.interp(p.z, ZS, DM if side < 0 else DP))*w)
    r_new = r_v + (Rs(th + dth, p.z) - Rs(th, p.z))
    th2 = th + dth
    q = mathutils.Vector((r_new*np.sin(th2), YC - r_new*np.cos(th2), p.z))
    maxd = max(maxd, (q - p).length); new[v.index] = q
for i, q in new.items(): bm.verts[i].co = q
bm.to_mesh(ob.data); bm.free(); ob.data.update()
bpy.app.driver_namespace['chambray_open'] = {'moved': len(new), 'max_move': maxd}
'''

S['ch_holes'] = r'''
import bmesh
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ch.data)
b = [e for e in bm.edges if e.is_boundary]
res = bmesh.ops.holes_fill(bm, edges=b, sides=20)
bmesh.ops.triangulate(bm, faces=res['faces'])
bm.to_mesh(ch.data); bm.free(); ch.data.update()
bpy.app.driver_namespace['ch_holes'] = len(res['faces'])
'''

S['hem_edges4'] = r'''
import bmesh, numpy as np, mathutils
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ch.data); bm.verts.ensure_lookup_table()
rl = bm.faces.layers.int.get('companion_region')
old_fold = [f for f in bm.faces if f[rl] == 13]
bmesh.ops.delete(bm, geom=old_fold, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bmesh.ops.dissolve_degenerate(bm, dist=2e-5, edges=list(bm.edges))
bm.normal_update()
bedges = [e for e in bm.edges if e.is_boundary]
adj = {}
for e in bedges:
    a, b = e.verts; adj.setdefault(a, []).append(e); adj.setdefault(b, []).append(e)
comps = []; seen = set()
for s in adj:
    if s in seen: continue
    comp = set(); st = [s]
    while st:
        u = st.pop()
        if u in comp: continue
        comp.add(u)
        for e in adj[u]:
            w = e.other_vert(u)
            if w not in comp: st.append(w)
    seen |= comp; comps.append(comp)
comp = max(comps, key=lambda c: sum(1 for v in c if v.co.z < 0.93))
main_edges = [e for e in bedges if e.verts[0] in comp and e.verts[1] in comp]
XM, XP = -0.026, -0.002
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]
DM = [9, 9, 11, 8, 6, 2, 0]; DP = [23, 23, 23, 21, 15, 5, 0]
def th_edge(side, z):
    if side < 0: return np.arctan2(XM, 0.15) - np.radians(np.interp(z, ZS, DM))
    return np.arctan2(XP, 0.15) + np.radians(np.interp(z, ZS, DP))
def is_edge(v):
    t = np.arctan2(v.co.x, -v.co.y)
    return min(abs(t - th_edge(-1, v.co.z)), abs(t - th_edge(1, v.co.z))) < np.radians(4.0)
hem = [v for v in comp if v.co.z < 0.93 and not is_edge(v)]
th = np.array([np.arctan2(v.co.x, -v.co.y) for v in hem]); zz = np.array([v.co.z for v in hem])
def wrapd(a, b): return np.abs((a - b + np.pi) % (2*np.pi) - np.pi)
zenv = np.array([zz[wrapd(th, t) < np.radians(5)].min() for t in th])
zsm = np.array([zenv[wrapd(th, t) < np.radians(3)].mean() for t in th])
moved = 0
for v, zt in zip(hem, zsm):
    if zt < v.co.z - 1e-5: v.co.z = zt; moved += 1
# panel bottom corners: edge verts below the local hem line join it
for v in comp:
    if is_edge(v) and v.co.z < 0.93:
        t = np.arctan2(v.co.x, -v.co.y)
        near = wrapd(th, t) < np.radians(8)
        if near.any():
            zt = zsm[near].min()
            if v.co.z < zt + 0.004: v.co.z = zt
band = [v for v in bm.verts if not v.is_boundary and v.co.z < 0.93 and v.co.y > -1 and any(w.is_boundary and w in comp for e in v.link_edges for w in e.verts) ]
ring2 = set(band)
for v in band:
    for e in v.link_edges:
        w = e.other_vert(v)
        if not w.is_boundary and w.co.z < 0.95: ring2.add(w)
for it in range(4):
    bmesh.ops.smooth_vert(bm, verts=list(ring2), factor=0.5, use_axis_x=False, use_axis_y=False, use_axis_z=True)
bm.normal_update()
edges = [e for e in main_edges if max(v.co.z for v in e.verts) < 1.345]
tin = {}; NRM = {}
for v in {v for e in edges for v in e.verts}:
    rad = mathutils.Vector((v.co.x, v.co.y, 0.0)); rad = rad.normalized() if rad.length > 1e-9 else mathutils.Vector((0,-1,0))
    NRM[v] = rad
    t = np.arctan2(v.co.x, -v.co.y)
    circ = mathutils.Vector((np.cos(t), np.sin(t), 0.0))   # direction of increasing theta
    de_m = abs(t - th_edge(-1, v.co.z)); de_p = abs(t - th_edge(1, v.co.z))
    if v.co.y < -0.04 and v.co.z > 0.86 and min(de_m, de_p) < np.radians(6):
        d = -circ if de_m < de_p else circ
        if v.co.z < 0.90: d = (d + mathutils.Vector((0,0,1))).normalized()
    else:
        d = mathutils.Vector((0,0,1))
    tin[v] = d
nb = {}
for e in edges:
    a, b = e.verts; nb.setdefault(a, []).append(b); nb.setdefault(b, []).append(a)
for it in range(3):
    new_t = {}
    for v, t in tin.items():
        acc = t*2.0
        for w in nb.get(v, []): acc = acc + tin[w]
        new_t[v] = acc.normalized() if acc.length > 1e-9 else t
    tin = new_t
r1 = bmesh.ops.extrude_edge_only(bm, edges=edges)
src_of = {}
for f in [g for g in r1['geom'] if isinstance(g, bmesh.types.BMFace)]:
    vs = list(f.verts); olds = [v for v in vs if v in tin]; news = [v for v in vs if v not in tin]
    for nv in news: src_of[nv] = min(olds, key=lambda o: (o.co - nv.co).length)
for nv, o in src_of.items(): nv.co = o.co - NRM[o]*0.0025
wall_edges = [g for g in r1['geom'] if isinstance(g, bmesh.types.BMEdge) and all(v in src_of for v in g.verts)]
r2 = bmesh.ops.extrude_edge_only(bm, edges=wall_edges)
for f in [g for g in r2['geom'] if isinstance(g, bmesh.types.BMFace)]:
    vs = list(f.verts); olds = [v for v in vs if v in src_of]; news = [v for v in vs if v not in src_of]
    for nv in news:
        o1 = min(olds, key=lambda o: (o.co - nv.co).length); o = src_of[o1]
        nv.co = o1.co + tin[o]*0.010 - NRM[o]*0.0005
for f in [g for g in r1['geom'] + r2['geom'] if isinstance(g, bmesh.types.BMFace)]:
    f[rl] = 13; f.smooth = True
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(ch.data); bm.free(); ch.data.update()
bpy.app.driver_namespace['hem_info'] = {'hem_verts': len(hem), 'moved_down': moved, 'fold_edges': len(edges), 'loops': [len(c) for c in comps][:6]}
'''

S['interior'] = r'''
import bmesh, numpy as np
TH, ZZ, R = bpy.app.driver_namespace['shell_grid']
def Rs(t, z):
    t = (t + np.pi) % (2*np.pi) - np.pi
    fi = np.clip((z - ZZ[0])/(ZZ[1]-ZZ[0]), 0, len(ZZ)-1.001); fj = np.clip((t - TH[0])/(TH[1]-TH[0]), 0, len(TH)-1.001)
    i0 = int(fi); j0 = int(fj); a = fi - i0; b = fj - j0
    return (R[i0,j0]*(1-a)*(1-b) + R[i0+1,j0]*a*(1-b) + R[i0,j0+1]*(1-a)*b + R[i0+1,j0+1]*a*b)
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ch.data)
rl = bm.faces.layers.int.get('companion_region')
kill = []
for f in bm.faces:
    if f[rl] == 13: continue
    c = f.calc_center_median()
    if c.z > 0.97 or c.y > -0.02: continue
    t = np.arctan2(c.x, -c.y); r = np.hypot(c.x, c.y)
    if r - Rs(t, c.z) < -0.012: kill.append(f)
bmesh.ops.delete(bm, geom=kill, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.faces.ensure_lookup_table(); seen = set(); small = []
for f in bm.faces:
    if f.index in seen: continue
    comp = [f]; seen.add(f.index); st = [f]
    while st:
        g = st.pop()
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen: seen.add(h.index); comp.append(h); st.append(h)
    if len(comp) < 200: small.extend(comp)
bmesh.ops.delete(bm, geom=small, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(ch.data); bm.free(); ch.data.update()
bpy.app.driver_namespace['interior_info'] = {'interior': len(kill), 'fragments': len(small)}
'''

S['flatten_trim'] = r'''
import bmesh, numpy as np
TH, ZZ, R = bpy.app.driver_namespace['shell_grid']
def Rs(t, z):
    t = (t + np.pi) % (2*np.pi) - np.pi
    fi = np.clip((z - ZZ[0])/(ZZ[1]-ZZ[0]), 0, len(ZZ)-1.001); fj = np.clip((t - TH[0])/(TH[1]-TH[0]), 0, len(TH)-1.001)
    i0 = int(fi); j0 = int(fj); a = fi - i0; b = fj - j0
    return (R[i0,j0]*(1-a)*(1-b) + R[i0+1,j0]*a*(1-b) + R[i0,j0+1]*(1-a)*b + R[i0+1,j0+1]*a*b)
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ch.data)
rl = bm.faces.layers.int.get('companion_region')
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[rl] == 13], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
trim_f = [f for f in bm.faces if f[rl] == 9]
tv = {v for f in trim_f for v in f.verts}
# local offset of the surrounding fabric from the smooth shell
def offset(v):
    t = np.arctan2(v.co.x, -v.co.y); return np.hypot(v.co.x, v.co.y) - Rs(t, v.co.z)
ring = set()
for v in tv:
    for e in v.link_edges:
        w = e.other_vert(v)
        if w not in tv: ring.add(w)
moved = 0
for v in tv:
    near = [w for w in ring if (w.co - v.co).length < 0.02]
    off = np.median([offset(w) for w in near]) if near else 0.0
    t = np.arctan2(v.co.x, -v.co.y); r = Rs(t, v.co.z) + off
    v.co.x = r*np.sin(t); v.co.y = -r*np.cos(t); moved += 1
top_idx = 0
for f in trim_f: f[rl] = 2; f.material_index = top_idx
bm.to_mesh(ch.data); bm.free(); ch.data.update()
bpy.app.driver_namespace['flatten_info'] = {'trim_faces': len(trim_f), 'verts': moved}
'''

S['edge_snap'] = r'''
import bmesh, numpy as np
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ch.data)
rl = bm.faces.layers.int.get('companion_region')
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[rl] == 13], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
XM, XP = -0.026, -0.002
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]
DM = [9, 9, 11, 8, 6, 2, 0]; DP = [23, 23, 23, 21, 15, 5, 0]
def te(side, z):
    return (np.arctan2(XM, 0.15) - np.radians(np.interp(z, ZS, DM))) if side < 0 else (np.arctan2(XP, 0.15) + np.radians(np.interp(z, ZS, DP)))
def theta(p): return np.arctan2(p.x, -p.y)
kill = []
for f in bm.faces:
    c = f.calc_center_median()
    if c.y > -0.05 or c.z < 0.86 or c.z > 1.335: continue
    t = theta(c)
    side = -1 if c.x < (XM + XP)/2 + 0.02*0 else 1
    # beyond the edge line, into the gap
    if side < 0 and t > te(-1, c.z) + np.radians(0.6): kill.append(f)
    if side > 0 and t < te(1, c.z) - np.radians(0.6): kill.append(f)
bmesh.ops.delete(bm, geom=kill, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.faces.ensure_lookup_table(); seen = set(); small = []
for f in bm.faces:
    if f.index in seen: continue
    comp = [f]; seen.add(f.index); st = [f]
    while st:
        g = st.pop()
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen: seen.add(h.index); comp.append(h); st.append(h)
    if len(comp) < 200: small.extend(comp)
bmesh.ops.delete(bm, geom=small, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
snapped = 0
for v in bm.verts:
    if not v.is_boundary or v.co.y > -0.05 or not (0.875 < v.co.z < 1.33): continue
    t = theta(v.co); r = np.hypot(v.co.x, v.co.y)
    for side in (-1, 1):
        tl = te(side, v.co.z)
        if abs(t - tl) < 0.015/r:
            v.co.x = r*np.sin(tl); v.co.y = -r*np.cos(tl); snapped += 1
bm.to_mesh(ch.data); bm.free(); ch.data.update()
bpy.app.driver_namespace['snap_info'] = {'beyond': len(kill), 'fragments': len(small), 'snapped': snapped}
'''

S['v_shards'] = r'''
import bmesh, numpy as np
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
bm = bmesh.new(); bm.from_mesh(me)
def inbox(c): return c.y < -0.02 and 1.318 < c.z < 1.41 and -0.07 < c.x < 0.03
kill = []
for f in bm.faces:
    if f.material_index != 0: continue
    c = f.calc_center_median()
    if not inbox(c): continue
    if -0.002 < c.x < 0.02 and 1.326 < c.z < 1.343 and c.y < -0.04:
        kill.append(f); continue
    if len(f.verts) == 3:
        es = [e.calc_length() for e in f.edges]; a = f.calc_area()
        if a > 0:
            h = 2*a/max(es)
            if max(es)/max(h, 1e-9) > 9: kill.append(f)
bmesh.ops.delete(bm, geom=kill, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
# drop tiny loose pieces created by the clean-up
bm.faces.ensure_lookup_table(); seen = set(); small = []
for f in bm.faces:
    if f.index in seen: continue
    comp = [f]; seen.add(f.index); st = [f]
    while st:
        g = st.pop()
        for e in g.edges:
            for h_ in e.link_faces:
                if h_.index not in seen: seen.add(h_.index); comp.append(h_); st.append(h_)
    if len(comp) < 30: small.extend(comp)
bmesh.ops.delete(bm, geom=small, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bnd = [v for v in bm.verts if v.is_boundary and inbox(v.co) and any(f.material_index == 0 for f in v.link_faces)]
for it in range(5):
    bmesh.ops.smooth_vert(bm, verts=bnd, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
bm.to_mesh(me); bm.free(); me.update()
bpy.app.driver_namespace['v_shards'] = {'deleted': len(kill), 'small': len(small), 'smoothed': len(bnd)}
'''

S['fill_small'] = r'''
import bmesh
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
bm = bmesh.new(); bm.from_mesh(me)
b = [e for e in bm.edges if e.is_boundary and all(f.material_index == 0 for f in e.link_faces)]
res = bmesh.ops.holes_fill(bm, edges=b, sides=14)
for f in res['faces']: f.material_index = 0; f.smooth = True
tri = bmesh.ops.triangulate(bm, faces=res['faces'])
bm.to_mesh(me); bm.free(); me.update()
bpy.app.driver_namespace['fill_small'] = len(res['faces'])
'''

S['strap_clamp'] = r'''
import numpy as np, mathutils, bmesh
from mathutils.bvhtree import BVHTree
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
bm = bmesh.new(); bm.from_mesh(me)
kill = []
for f in bm.faces:
    if f.material_index != 0: continue
    c = f.calc_center_median()
    if c.y < -0.04 and 1.322 < c.z < 1.375 and -0.029 < c.x < 0.013:
        kill.append(f)
bmesh.ops.delete(bm, geom=kill, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(me); bm.free(); me.update()
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
shirt_tris = [tuple(p.vertices) for p in me.polygons if fm[p.index] == 0]
shirt = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], shirt_tris)
body = bpy.data.objects['Arjun_Body']; bm_ = body.data
regb = np.empty(len(bm_.polygons), np.int32); bm_.attributes['companion_region'].data.foreach_get('value', regb)
skin = BVHTree.FromPolygons([v.co.copy() for v in bm_.vertices], [tuple(p.vertices) for i,p in enumerate(bm_.polygons) if regb[i] == 0])
tee_v = sorted({vi for p in me.polygons if fm[p.index] == 1 for vi in p.vertices})
moved = 0; conflicts = 0
for vi in tee_v:
    v = me.vertices[vi]; p = v.co.copy()
    if p.z < 1.30 or p.z > 1.49 or p.y > 0.03: continue
    axis = mathutils.Vector((0.0, 0.02, p.z))
    d = p - axis; r = d.length
    if r < 1e-6: continue
    d /= r
    hs = skin.ray_cast(axis + d*0.25, -d, 0.25)
    rs = (hs[0] - axis).length if hs[0] is not None else 0.0
    hc = shirt.ray_cast(axis + d*0.002, d, 0.3)
    rc = (hc[0] - axis).length if hc[0] is not None else 9.0
    lo, hi = rs + 0.0035, rc - 0.003
    if lo > hi:
        conflicts += 1; target = (lo + hi)/2
    else:
        target = min(max(r, lo), hi)
    if abs(target - r) > 1e-5:
        v.co = axis + d*target; moved += 1
me.update()
bpy.app.driver_namespace['strap_clamp'] = {'strap_faces': len(kill), 'tee_moved': moved, 'conflicts': conflicts}
'''

# ==================================================================================================
# ---- 13e Tee (lofted rings: tucked into the waistband, crew rib at the neck) ----
# Rings follow a body ellipse clamped 6 mm inside the smooth shell. Clearance is enforced along rays from
# the torso/neck axis (skin + 3.5 mm, shirt - 3 mm); nearest-point pushes jumped up to 5 cm. Hidden parts
# (back half, armpits, under the panels) are inset or removed so extreme poses do not poke through.
S['tee_loft'] = r'''
import numpy as np, bmesh, mathutils
from mathutils.bvhtree import BVHTree
TH, ZZ, R = bpy.app.driver_namespace['shell_grid']
def Rs(t, z):
    t = (t + np.pi) % (2*np.pi) - np.pi
    fi = np.clip((z - ZZ[0])/(ZZ[1]-ZZ[0]), 0, len(ZZ)-1.001); fj = np.clip((t - TH[0])/(TH[1]-TH[0]), 0, len(TH)-1.001)
    i0 = int(fi); j0 = int(fj); a = fi - i0; b = fj - j0
    return (R[i0,j0]*(1-a)*(1-b) + R[i0+1,j0]*a*(1-b) + R[i0,j0+1]*(1-a)*b + R[i0+1,j0+1]*a*b)
BZ = [0.95, 1.00, 1.10, 1.20, 1.30]
BA = [0.150, 0.150, 0.153, 0.160, 0.168]
BF = [0.106, 0.108, 0.114, 0.120, 0.122]
BB = [0.122, 0.122, 0.123, 0.127, 0.132]
def r_body(t, z):
    a = np.interp(z, BZ, BA); b = np.interp(z, BZ, BF) if abs(t) < np.pi/2 else np.interp(z, BZ, BB)
    return 1.0/np.sqrt((np.sin(t)/a)**2 + (np.cos(t)/b)**2)
def ease(z): return float(np.interp(z, [0.95, 0.997, 1.006, 1.02, 1.10, 1.20, 1.30], [0.0005, 0.0005, 0.005, 0.008, 0.008, 0.005, 0.004]))
N = 144
TJ = np.linspace(-np.pi, np.pi, N, endpoint=False)   # seam at the back
rings = []
zs = list(np.arange(0.955, 1.3001, 0.01))
for z in zs:
    ring = []
    for t in TJ:
        r = r_body(t, z) + ease(z)
        rs = Rs(t, z)
        if np.isfinite(rs): r = min(r, rs - 0.006)
        ring.append(mathutils.Vector((r*np.sin(t), -r*np.cos(t), z)))
    rings.append(ring)
# neckline ellipse around the neck axis (0, 0.02), tilted from front (low) to back (high)
NA = 0.064; NBF = 0.061; NBB = 0.066
def neck_pt(t, dr=0.0, dz=0.0):
    b = NBF if abs(t) < np.pi/2 else NBB
    r = 1.0/np.sqrt((np.sin(t)/NA)**2 + (np.cos(t)/b)**2) + dr
    zn = 1.405 + (1.462 - 1.405)*(1 - np.cos(t))/2 + dz
    return mathutils.Vector((r*np.sin(t), 0.02 - r*np.cos(t), zn))
P0 = rings[-1]
K = 9
for k in range(1, K+1):
    s = k/(K+1)
    ring = []
    for j, t in enumerate(TJ):
        p0 = P0[j]; p1 = neck_pt(t)
        q = p0*(1-s) + p1*s
        radial = mathutils.Vector((q.x, q.y - 0.01, 0.0))
        if radial.length > 1e-6: q = q + radial.normalized()*(0.004*4*s*(1-s))
        ring.append(q)
    rings.append(ring)
rings.append([neck_pt(t) for t in TJ])
rings.append([neck_pt(t, -0.004, 0.014) for t in TJ])   # rib band top
rings.append([neck_pt(t, -0.0075, 0.0165) for t in TJ])  # rolled lip
rings.append([neck_pt(t, -0.009, 0.004) for t in TJ])    # inner facing
# push: stay >= 3 mm outside the skin, >= 4 mm inside the chambray shirt
body = bpy.data.objects['Arjun_Body']; bmr = np.empty(len(body.data.polygons), np.int32); body.data.attributes['companion_region'].data.foreach_get('value', bmr)
skin = BVHTree.FromPolygons([v.co.copy() for v in body.data.vertices], [tuple(p.vertices) for i,p in enumerate(body.data.polygons) if bmr[i] == 0])
ch = bpy.data.objects['Arjun_Shirt_Chambray'].data
shirt = BVHTree.FromPolygons([v.co.copy() for v in ch.vertices], [tuple(p.vertices) for p in ch.polygons])
nfix_s = nfix_c = 0
for k, ring in enumerate(rings[:-3]):
    for j, p in enumerate(ring):
        loc, nrm, idx, d = skin.find_nearest(p)
        if loc is not None and d < 0.003 + 0.001:
            out = (p - loc); out = out.normalized() if out.length > 1e-6 else nrm
            if out.dot(nrm) < 0: out = nrm
            ring[j] = loc + out*0.0035; nfix_s += 1

me = bpy.data.meshes.get('Arjun_Tee_tmp') or bpy.data.meshes.new('Arjun_Tee_tmp')
bm = bmesh.new()
V = [[bm.verts.new(p) for p in ring] for ring in rings]
uvl = bm.loops.layers.uv.new('UVMap')
# arc-length v coordinate along the profile at the front
acc = [0.0]
for k in range(1, len(rings)):
    acc.append(acc[-1] + (rings[k][N//2] - rings[k-1][N//2]).length)
for k in range(len(rings)-1):
    for j in range(N):
        j2 = (j+1) % N
        f = bm.faces.new((V[k][j], V[k][j2], V[k+1][j2], V[k+1][j]))
        for l, (kk, jj) in zip(f.loops, ((k, j), (k, j+1), (k+1, j+1), (k+1, j))):
            l[uvl].uv = (jj/N, acc[kk]/acc[-1])
bm.normal_update()
bm.to_mesh(me); bm.free()
ob = bpy.data.objects.get('Arjun_Tee_tmp') or bpy.data.objects.new('Arjun_Tee_tmp', me)
if ob.name not in bpy.data.collections['Companion_Work'].objects: bpy.data.collections['Companion_Work'].objects.link(ob)
ob.parent = bpy.data.objects['Arjun_Rig']; ob.matrix_parent_inverse.identity()
for p in me.polygons: p.use_smooth = True
# outward normals: torso rings wind so that faces face outward
me.update()
bpy.app.driver_namespace['tee_info'] = {'rings': len(rings), 'verts': len(me.vertices), 'skin_push': nfix_s, 'shirt_push': nfix_c}
'''

S['tee_restore_push'] = r'''
import numpy as np, mathutils
from mathutils.bvhtree import BVHTree
src_path = 'D:/Blender/Companion_Outfits_20261009_Arjun/checkpoints/arjun-outfits-before-merge.blend'
with bpy.data.libraries.load(src_path, link=False) as (df, dt):
    dt.meshes = ['Arjun_Tee_tmp']
tm = dt.meshes[0]
orig = np.empty(len(tm.vertices)*3, np.float32); tm.vertices.foreach_get('co', orig); orig = orig.reshape(-1,3)
for m_ in list(tm.materials):
    pass
bpy.data.meshes.remove(tm)
for d in list(bpy.data.materials):
    if d.users == 0 and d.name.startswith('review_white.'): bpy.data.materials.remove(d)
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
TF = 13150
for k in range(len(orig)):
    me.vertices[TF + k].co = orig[k]
# radial clearance from the neck axis: tee >= skin + 3.5 mm along the same ray
body = bpy.data.objects['Arjun_Body']; bm_ = body.data
regb = np.empty(len(bm_.polygons), np.int32); bm_.attributes['companion_region'].data.foreach_get('value', regb)
skin = BVHTree.FromPolygons([v.co.copy() for v in bm_.vertices], [tuple(p.vertices) for i,p in enumerate(bm_.polygons) if regb[i] == 0])
pushed = 0; worst = 0.0
for k in range(len(orig)):
    v = me.vertices[TF + k]; p = v.co
    if p.z < 1.30 or p.z > 1.49 or p.y > 0.03: continue
    axis = mathutils.Vector((0.0, 0.02, p.z))
    d = mathutils.Vector((p.x, p.y, p.z)) - axis
    r = d.length
    if r < 1e-6: continue
    d /= r
    hit = skin.ray_cast(axis + d*0.25, -d, 0.25)
    if hit[0] is None: continue
    rs = (hit[0] - axis).length
    if r < rs + 0.0035:
        worst = max(worst, rs + 0.0035 - r)
        v.co = axis + d*(rs + 0.0035); pushed += 1
me.update()
bpy.app.driver_namespace['tee_push'] = {'pushed': pushed, 'worst_mm': round(worst*1000, 1)}
'''

S['smooth_fix'] = r'''
import bmesh, numpy as np, mathutils
from mathutils.bvhtree import BVHTree
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
tee_v = sorted({vi for p in me.polygons if fm[p.index] == 1 for vi in p.vertices})
TF = tee_v[0]
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
# tee: upper front, below the rib band (ring index < 43)
sel = [bm.verts[i] for i in tee_v if 1.22 < bm.verts[i].co.z and (i - TF)//144 < 43 and bm.verts[i].co.y < 0.0]
for it in range(8):
    bmesh.ops.smooth_vert(bm, verts=sel, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
# shirt: flatten the old button notches along the pocket-side edge
XP = -0.002; ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]; DP = [23, 23, 23, 21, 15, 5, 0]
def te_p(z): return np.arctan2(XP, 0.15) + np.radians(np.interp(z, ZS, DP))
notch = set()
for f in bm.faces:
    if f.material_index != 0: continue
    for v in f.verts:
        t = np.arctan2(v.co.x, -v.co.y); r = np.hypot(v.co.x, v.co.y)
        if v.co.y < -0.05 and v.co.x > -0.01 and 0 <= (t - te_p(v.co.z))*r < 0.012 and any(abs(v.co.z - zb) < 0.012 for zb in (1.294, 1.199, 1.113, 1.031, 0.949)):
            notch.add(v)
inner = [v for v in notch if not v.is_boundary]
for it in range(8):
    bmesh.ops.smooth_vert(bm, verts=inner, factor=0.6, use_axis_x=True, use_axis_y=True, use_axis_z=True)
bnd = [v for v in notch if v.is_boundary]
for it in range(4):
    bmesh.ops.smooth_vert(bm, verts=bnd, factor=0.5, use_axis_x=False, use_axis_y=False, use_axis_z=True)
for v in bnd:
    r = np.hypot(v.co.x, v.co.y); t = te_p(v.co.z)
    v.co.x = r*np.sin(t); v.co.y = -r*np.cos(t)
bm.to_mesh(me); bm.free(); me.update()
# clearance again: tee >= skin + 3.5 mm and <= shirt - 3 mm along rays from the torso/neck axis
body = bpy.data.objects['Arjun_Body']; bm_ = body.data
regb = np.empty(len(bm_.polygons), np.int32); bm_.attributes['companion_region'].data.foreach_get('value', regb)
skin = BVHTree.FromPolygons([v.co.copy() for v in bm_.vertices], [tuple(p.vertices) for i,p in enumerate(bm_.polygons) if regb[i] == 0])
shirt = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons if fm[p.index] == 0])
moved = 0
for vi in tee_v:
    v = me.vertices[vi]; p = v.co.copy()
    if p.z < 1.0 or p.y > 0.03: continue
    axis = mathutils.Vector((0.0, 0.02 if p.z > 1.30 else 0.0, p.z))
    d = p - axis; r = d.length
    if r < 1e-6: continue
    d /= r
    hs = skin.ray_cast(axis + d*0.25, -d, 0.25)
    rs = (hs[0] - axis).length if hs[0] is not None else 0.0
    hc = shirt.ray_cast(axis + d*0.002, d, 0.3)
    rc = (hc[0] - axis).length if hc[0] is not None else 9.0
    lo, hi = rs + 0.0035, rc - 0.003
    target = (lo + hi)/2 if lo > hi else min(max(r, lo), hi)
    if abs(target - r) > 1e-5: v.co = axis + d*target; moved += 1
me.update()
bpy.app.driver_namespace['smooth_fix'] = {'tee_smoothed': len(sel), 'notch_inner': len(inner), 'notch_edge': len(bnd), 'clamped': moved}
'''

S['tee_sides'] = r'''
import numpy as np
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
tee_v = sorted({vi for p in me.polygons if fm[p.index] == 1 for vi in p.vertices})
def ss(e0, e1, v):
    t = np.clip((v - e0)/(e1 - e0), 0, 1); return t*t*(3 - 2*t)
n = 0
for vi in tee_v:
    v = me.vertices[vi]; x, y, z = v.co
    if z < 1.10 or z > 1.40: continue
    r = np.hypot(x, y)
    if r < 1e-6: continue
    side = abs(x)/r
    w = ss(0.70, 0.92, side)*ss(1.12, 1.20, z)*(1 - ss(1.33, 1.40, z))
    if w <= 0: continue
    k = (r - 0.009*w)/r
    v.co.x = x*k; v.co.y = y*k; n += 1
me.update()
bpy.app.driver_namespace['tee_sides'] = n
'''

S['tee_upper2'] = r'''
import numpy as np, mathutils
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
tee_v = sorted({vi for p in me.polygons if fm[p.index] == 1 for vi in p.vertices})
TF = tee_v[0]
def ss(e0, e1, v):
    t = np.clip((v - e0)/(e1 - e0), 0, 1); return t*t*(3 - 2*t)
n = 0
for vi in tee_v:
    if (vi - TF)//144 >= 43: continue          # leave the rib band and neckline alone
    v = me.vertices[vi]; x, y, z = v.co
    if z < 1.20: continue
    w = ss(0.062, 0.088, abs(x))*ss(1.24, 1.28, z)*(1 - ss(1.38, 1.42, z))
    if w <= 0: continue
    axis = mathutils.Vector((0.0, 0.02 if z > 1.30 else 0.0, z))
    d = v.co - axis; r = d.length
    if r < 1e-6: continue
    v.co = axis + d*((r - 0.006*w)/r); n += 1
me.update()
bpy.app.driver_namespace['tee_upper'] = n
'''

S['tee_back'] = r'''
import bmesh, numpy as np
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
tee_v = sorted({vi for p in me.polygons if fm[p.index] == 1 for vi in p.vertices})
TF, TN = tee_v[0], len(tee_v)
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
kill = []
for f in bm.faces:
    if f.material_index != 1: continue
    if any((v.index - TF)//144 >= 42 for v in f.verts): continue
    c = f.calc_center_median()
    if c.y > 0.035: kill.append(f)
bmesh.ops.delete(bm, geom=kill, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(me); bm.free(); me.update()
bpy.app.driver_namespace['tee_back'] = {'faces_removed': len(kill), 'verts': len(me.vertices)}
'''

# ==================================================================================================
# ---- 13f Buttons and an accessory ----
S['buttons'] = r'''
import bmesh, numpy as np, mathutils
from mathutils.bvhtree import BVHTree
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bvh = BVHTree.FromPolygons([v.co.copy() for v in ch.data.vertices], [tuple(p.vertices) for p in ch.data.polygons])
XM = -0.026
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]; DM = [9, 9, 11, 8, 6, 2, 0]
mat = bpy.data.materials.get('Arjun_Chambray_Buttons') or bpy.data.materials.new('Arjun_Chambray_Buttons')
mat.diffuse_color = (0.95, 0.95, 0.93, 1)
me = bpy.data.meshes.get('Arjun_Buttons_tmp') or bpy.data.meshes.new('Arjun_Buttons_tmp')
bm = bmesh.new()
uvl = bm.loops.layers.uv.new('UVMap')
placed = []
SEG = 16; RAD = 0.0055; TH = 0.0024
for zb in (1.294, 1.199, 1.113, 1.031, 0.949):
    te = np.arctan2(XM, 0.15) - np.radians(np.interp(zb, ZS, DM))
    # step 13 mm into the panel along the surface (towards -x)
    r0 = 0.15
    tb = te - 0.013/r0
    d = mathutils.Vector((np.sin(tb), -np.cos(tb), 0.0)); o = mathutils.Vector((0.0, 0.0, zb))
    hit = bvh.ray_cast(o + d*0.4, -d, 0.4)
    if hit[0] is None: continue
    p, n = hit[0], hit[1]
    if n.dot(d) < 0: n = -n
    up = mathutils.Vector((0,0,1)); x_ax = up.cross(n).normalized(); y_ax = n.cross(x_ax).normalized()
    c0 = p + n*0.0003
    ring_b = []; ring_t = []; ring_r = []
    for k in range(SEG):
        a = 2*np.pi*k/SEG
        off = x_ax*np.cos(a) + y_ax*np.sin(a)
        ring_b.append(bm.verts.new(c0 + off*RAD))
        ring_t.append(bm.verts.new(c0 + off*RAD*0.98 + n*TH))
        ring_r.append(bm.verts.new(c0 + off*RAD*0.80 + n*(TH*0.92)))
    top = bm.verts.new(c0 + n*(TH*0.95))
    for k in range(SEG):
        k2 = (k+1) % SEG
        f = bm.faces.new((ring_b[k], ring_b[k2], ring_t[k2], ring_t[k]))
        for l in f.loops: l[uvl].uv = (0.02, 0.98)
        f = bm.faces.new((ring_t[k], ring_t[k2], ring_r[k2], ring_r[k]))
        for l in f.loops: l[uvl].uv = (0.02, 0.98)
        f = bm.faces.new((ring_r[k], ring_r[k2], top))
        for l, (u, v) in zip(f.loops, ((0.5+0.4*np.cos(2*np.pi*k/SEG), 0.5+0.4*np.sin(2*np.pi*k/SEG)), (0.5+0.4*np.cos(2*np.pi*k2/SEG), 0.5+0.4*np.sin(2*np.pi*k2/SEG)), (0.5, 0.5))):
            l[uvl].uv = (u, v)
    placed.append([round(x, 4) for x in c0])
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(me); bm.free()
me.materials.clear(); me.materials.append(mat)
ob = bpy.data.objects.get('Arjun_Buttons_tmp') or bpy.data.objects.new('Arjun_Buttons_tmp', me)
if ob.name not in bpy.data.collections['Companion_Work'].objects: bpy.data.collections['Companion_Work'].objects.link(ob)
ob.parent = bpy.data.objects['Arjun_Rig']; ob.matrix_parent_inverse.identity()
for p_ in me.polygons: p_.use_smooth = True
bpy.app.driver_namespace['buttons_info'] = placed
'''

S['watch'] = r'''
import bmesh, numpy as np, mathutils
from mathutils.bvhtree import BVHTree
body = bpy.data.objects['Arjun_Body']; me_b = body.data
reg = np.empty(len(me_b.polygons), np.int32); me_b.attributes['companion_region'].data.foreach_get('value', reg)
skin = BVHTree.FromPolygons([v.co.copy() for v in me_b.vertices], [tuple(p.vertices) for i,p in enumerate(me_b.polygons) if reg[i]==0])
bones = bpy.data.objects['Arjun_Rig'].data.bones
A = mathutils.Vector(bones['CC_Base_L_Forearm'].head_local); B = mathutils.Vector(bones['CC_Base_L_Forearm'].tail_local)
ax = (B - A).normalized()
u = (mathutils.Vector((0,0,1)) - ax*ax.z).normalized(); w = ax.cross(u).normalized()
XC = 0.577
c = A + ax*((XC - A.x)/ax.x)
N = 64; WID = 0.018; CLR = 0.0025; TH = 0.0026
def r_skin(a, s):
    d = u*np.cos(a) + w*np.sin(a); cc = c + ax*s
    hit = skin.ray_cast(cc + d*0.2, -d, 0.2)
    return (hit[0]-cc).length if hit[0] else 0.028
angles = np.linspace(0, 2*np.pi, N, endpoint=False)
rs = np.array([max(r_skin(a, -WID/2), r_skin(a, 0.0), r_skin(a, WID/2)) for a in angles])
rs = np.convolve(np.pad(rs, 2, mode='wrap'), np.ones(5)/5, 'valid')
bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
def P(a, r, s): return c + ax*s + (u*np.cos(a) + w*np.sin(a))*r
# bracelet: closed tube of rectangular section (inner, outer, two sides)
rings = []
for (rr, s) in ((0.0, -WID/2), (TH, -WID/2), (TH, WID/2), (0.0, WID/2)):
    rings.append([bm.verts.new(P(a, rs[j] + CLR + rr, s)) for j, a in enumerate(angles)])
for k in range(4):
    R0, R1 = rings[k], rings[(k+1) % 4]
    for j in range(N):
        j2 = (j+1) % N
        f = bm.faces.new((R0[j], R0[j2], R1[j2], R1[j])); f.material_index = 0
        for l, (jj, kk) in zip(f.loops, ((j, k), (j+1, k), (j+1, k+1), (j, k+1))):
            l[uvl].uv = (0.5 + 0.5*(jj/N), 0.5*((kk % 4)/4 + (0.25 if kk == 4 else 0)))
# case frame on top of the wrist
top_r = rs[0] + CLR + TH
c0 = c + u*top_r
X = ax; Y = w; Z = u
def Q(x, y, z): return c0 + X*x + Y*y + Z*z
SEG = 48
def ring(rad, z, shift=0.0):
    return [bm.verts.new(Q(rad*np.cos(2*np.pi*k/SEG + shift), rad*np.sin(2*np.pi*k/SEG + shift), z)) for k in range(SEG)]
def band(r0, r1, mat, uv):
    for k in range(SEG):
        k2 = (k+1) % SEG
        f = bm.faces.new((r0[k], r0[k2], r1[k2], r1[k])); f.material_index = mat
        for l in f.loops: l[uvl].uv = uv
RC = 0.017
b0 = ring(RC*0.93, -0.0006); b1 = ring(RC, 0.0012); b2 = ring(RC, 0.0055); b3 = ring(RC*0.98, 0.0068)
z1 = ring(RC*0.94, 0.0080); z2 = ring(RC*0.80, 0.0082); z3 = ring(RC*0.78, 0.0070)
STEEL_UV = (0.75, 0.9)
band(b0, b1, 0, STEEL_UV); band(b1, b2, 0, STEEL_UV); band(b2, b3, 0, STEEL_UV); band(b3, z1, 0, STEEL_UV); band(z1, z2, 0, STEEL_UV); band(z2, z3, 0, STEEL_UV)
# case back
f = bm.faces.new(b0[::-1]); f.material_index = 0
for l in f.loops: l[uvl].uv = STEEL_UV
# dial (flat, slightly below the bezel) with polar UVs into the left half of the atlas
cen = bm.verts.new(Q(0, 0, 0.0071))
for k in range(SEG):
    k2 = (k+1) % SEG
    f = bm.faces.new((z3[k], z3[k2], cen)); f.material_index = 1
    for l, (a, rr) in zip(f.loops, ((2*np.pi*k/SEG, 1.0), (2*np.pi*k2/SEG, 1.0), (0.0, 0.0))):
        l[uvl].uv = (0.25 + 0.24*rr*np.cos(a), 0.5 + 0.24*rr*np.sin(a))
# crown on the hand side (3 o'clock)
cr = [bm.verts.new(Q(RC + 0.0002 + 0.0032*t, 0.0022*np.cos(2*np.pi*k/12), 0.0034 + 0.0022*np.sin(2*np.pi*k/12))) for t in (0.0, 1.0) for k in range(12)]
for k in range(12):
    k2 = (k+1) % 12
    f = bm.faces.new((cr[k], cr[k2], cr[12+k2], cr[12+k])); f.material_index = 0
    for l in f.loops: l[uvl].uv = STEEL_UV
f = bm.faces.new(cr[12:][::-1]); f.material_index = 0
for l in f.loops: l[uvl].uv = STEEL_UV
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
me = bpy.data.meshes.get('Arjun_Watch') or bpy.data.meshes.new('Arjun_Watch')
bm.to_mesh(me); bm.free()
ob = bpy.data.objects.get('Arjun_Watch') or bpy.data.objects.new('Arjun_Watch', me)
if ob.name not in bpy.data.collections['Companion_Work'].objects: bpy.data.collections['Companion_Work'].objects.link(ob)
ob.parent = bpy.data.objects['Arjun_Rig']; ob.matrix_parent_inverse.identity()
me.materials.clear()
for n, col in (('Arjun_Watch_Steel', (0.78,0.79,0.8,1)), ('Arjun_Watch_Dial', (0.06,0.07,0.09,1))):
    m = bpy.data.materials.get(n) or bpy.data.materials.new(n); m.diffuse_color = col; me.materials.append(m)
for p in me.polygons: p.use_smooth = True
bpy.app.driver_namespace['watch_info'] = {'verts': len(me.vertices), 'case_center': [round(v,4) for v in c0], 'wrist_r_top': round(float(rs[0]),4)}
'''

# ==================================================================================================
# ---- 13g Weights and garment spring chains ----
# Garment chain bones belong to the garment (grafted under CC_Base bones at Unity import). Opened panels must
# be re-weighted from the source shirt at their NEW positions (same source as the tee) or the tee pokes
# through when the arms rise. Drop weights to bones a garment does not export (hair) and refill from
# neighbours, or Unity pins those vertices to the root bone.
S['w_helpers'] = r'''
import numpy as np, mathutils, bmesh
from mathutils.bvhtree import BVHTree
def ss(e0, e1, v):
    t = np.clip((v - e0)/(e1 - e0), 0, 1); return t*t*(3 - 2*t)
def read_w(ob):
    me = ob.data; names = [g.name for g in ob.vertex_groups]
    W = [dict() for _ in range(len(me.vertices))]
    for v in me.vertices:
        for g in v.groups:
            if g.weight > 0: W[v.index][names[g.group]] = g.weight
    return W
def write_w(ob, W, limit=4):
    for g in list(ob.vertex_groups): ob.vertex_groups.remove(g)
    groups = {}
    for i, w in enumerate(W):
        items = sorted(((k, x) for k, x in w.items() if x > 0), key=lambda kv: -kv[1])[:limit]
        tot = sum(x for _, x in items)
        if tot <= 0: continue
        for k, x in items:
            x = x/tot
            if x < 0.01: continue
            if k not in groups: groups[k] = ob.vertex_groups.new(name=k)
            groups[k].add([i], round(x, 3), 'REPLACE')
def blend(a, b, t):
    out = {}
    for k, x in a.items(): out[k] = out.get(k, 0) + x*(1 - t)
    for k, x in b.items(): out[k] = out.get(k, 0) + x*t
    return out
def strip(w, prefixes):
    out = {k: x for k, x in w.items() if not any(k.startswith(p) for p in prefixes)}
    tot = sum(out.values())
    return {k: x/tot for k, x in out.items()} if tot > 0 else {'CC_Base_Hip': 1.0}
def interp_w(ob, W, bvh_tris, p):
    loc, nrm, idx, d = bvh_tris[0].find_nearest(p)
    tri = bvh_tris[1][idx]
    a, b, c = (ob.data.vertices[i].co for i in tri)
    bc = mathutils.geometry.barycentric_transform(loc, a, b, c, mathutils.Vector((1,0,0)), mathutils.Vector((0,1,0)), mathutils.Vector((0,0,1)))
    out = {}
    for wi, vi in zip(bc, tri):
        wi = max(0.0, wi)
        for k, x in W[vi].items(): out[k] = out.get(k, 0) + x*wi
    tot = sum(out.values())
    return {k: x/tot for k, x in out.items()} if tot > 0 else {}
def tri_bvh(ob):
    me = ob.data; me.calc_loop_triangles()
    tris = [tuple(t.vertices) for t in me.loop_triangles]
    return (BVHTree.FromPolygons([v.co.copy() for v in me.vertices], tris), tris)
WBAND = {'CC_Base_Hip': 0.55, 'CC_Base_Pelvis': 0.25, 'CC_Base_Waist': 0.20}
XM, XP = -0.026, -0.002
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]; DM = [9, 9, 11, 8, 6, 2, 0]; DP = [23, 23, 23, 21, 15, 5, 0]
def th_edge(side, z):
    return (np.arctan2(XM, 0.15) - np.radians(np.interp(z, ZS, DM))) if side < 0 else (np.arctan2(XP, 0.15) + np.radians(np.interp(z, ZS, DP)))
TH, ZZ, R = bpy.app.driver_namespace['shell_grid']
def Rs(t, z):
    t = (t + np.pi) % (2*np.pi) - np.pi
    fi = np.clip((z - ZZ[0])/(ZZ[1]-ZZ[0]), 0, len(ZZ)-1.001); fj = np.clip((t - TH[0])/(TH[1]-TH[0]), 0, len(TH)-1.001)
    i0 = int(fi); j0 = int(fj); a = fi - i0; b = fj - j0
    return (R[i0,j0]*(1-a)*(1-b) + R[i0+1,j0]*a*(1-b) + R[i0,j0+1]*(1-a)*b + R[i0+1,j0+1]*a*b)
'''

S['w_bones'] = r'''
rig = bpy.data.objects['Arjun_Rig']
for o in bpy.context.view_layer.objects: o.select_set(False)
rig.select_set(True); bpy.context.view_layer.objects.active = rig
with bpy.context.temp_override(active_object=rig, object=rig):
    bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones
def put(name, head, tail, parent):
    b = eb.get(name) or eb.new(name)
    b.head = head; b.tail = tail; b.parent = eb[parent]; b.use_deform = True; b.use_connect = False
    b.roll = 0.0
    return b
def surf(t, z, inset=0.0):
    r = Rs(t, z) + inset
    return mathutils.Vector((r*np.sin(t), -r*np.cos(t), z))
made = []
for side, nm in ((-1, 'Chambray_FR'), (1, 'Chambray_FL')):
    zs = [1.12, 1.02, 0.93, 0.845]
    pts = []
    for z in zs:
        r = Rs(th_edge(side, z), z)
        t = th_edge(side, z) + side*(0.015/r)
        pts.append(surf(t, z, -0.004))
    parent = 'CC_Base_Spine01'
    for k in range(3):
        b = put(f'{nm}_0{k+1}', pts[k], pts[k+1], parent if k == 0 else f'{nm}_0{k}')
        if k > 0: b.use_connect = True
        made.append(b.name)
for src in ('L', 'BL', 'B', 'BR', 'R'):
    for k in (1, 2):
        s = eb[f'Shirt_{src}_0{k}']
        b = put(f'Chambray_{src}_0{k}', s.head.copy(), s.tail.copy(), 'CC_Base_Hip' if k == 1 else f'Chambray_{src}_01')
        if k == 2: b.use_connect = True
        made.append(b.name)
with bpy.context.temp_override(active_object=rig, object=rig):
    bpy.ops.object.mode_set(mode='OBJECT')
dyn = rig.data.collections.get('Dynamics')
if dyn:
    for n in made: dyn.assign(rig.data.bones[n])
bpy.app.driver_namespace['chambray_bones'] = made
'''

S['w_chambray2'] = r'''
classic = bpy.data.objects['Arjun_Shirt_Classic']; Wc = read_w(classic); bc = tri_bvh(classic)
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
W = read_w(ch)
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
fabric = sorted({vi for p in me.polygons if fm[p.index] == 0 for vi in p.vertices})
buttons = sorted({vi for p in me.polygons if fm[p.index] == 2 for vi in p.vertices})
co = np.array([v.co[:] for v in me.vertices])
bones = bpy.data.objects['Arjun_Rig'].data.bones
hem_ch = {n: np.arctan2(bones[f'Chambray_{n}_01'].head_local.x, -bones[f'Chambray_{n}_01'].head_local.y) for n in ('L','BL','B','BR','R')}
def wrapd(a, b): return abs((a - b + np.pi) % (2*np.pi) - np.pi)
for i in fabric:
    x, y, z = co[i]
    base = strip(interp_w(classic, Wc, bc, mathutils.Vector(co[i])), ('Shirt_', 'Hair_'))
    t = np.arctan2(x, -y)
    chains = {}
    for side, nm in ((-1, 'FR'), (1, 'FL')):
        if y > -0.02 or z > 1.12: continue
        d = wrapd(t, th_edge(side, z))
        fall = 1 - ss(np.radians(14), np.radians(40), d)
        infl = 0.92*ss(1.12, 0.95, z)*fall
        if infl <= 0: continue
        w1 = ss(1.07, 1.00, z); w2 = ss(0.98, 0.90, z)
        for k, v in ((f'Chambray_{nm}_01', 1 - w1), (f'Chambray_{nm}_02', w1 - w2), (f'Chambray_{nm}_03', w2)):
            if v > 0: chains[k] = chains.get(k, 0) + infl*v
    if z < 0.985:
        angs = dict(hem_ch); angs['FRe'] = th_edge(-1, z); angs['FLe'] = th_edge(1, z)
        (da, na), (db, nb) = sorted(((wrapd(t, a), n) for n, a in angs.items()))[:2]
        wa = db/(da + db + 1e-9); wb = 1 - wa
        infl = 0.95*ss(0.985, 0.885, z); w01 = np.clip(1 - (0.915 - z)/0.07, 0, 1)
        for n, ww in ((na, wa), (nb, wb)):
            if n in ('FRe', 'FLe'): continue
            chains[f'Chambray_{n}_01'] = chains.get(f'Chambray_{n}_01', 0) + infl*ww*w01
            chains[f'Chambray_{n}_02'] = chains.get(f'Chambray_{n}_02', 0) + infl*ww*(1 - w01)
    tot = sum(chains.values())
    if tot > 0.95: chains = {k: v*0.95/tot for k, v in chains.items()}; tot = 0.95
    w = {k: v*(1 - tot) for k, v in base.items()}
    for k, v in chains.items(): w[k] = w.get(k, 0) + v
    W[i] = w
# buttons: rigid copy of the fabric under each button
fab = np.array(fabric)
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
bset = set(buttons); seen = set()
for vi in buttons:
    if vi in seen: continue
    comp = [vi]; seen.add(vi); st = [bm.verts[vi]]
    while st:
        u = st.pop()
        for e in u.link_edges:
            w_ = e.other_vert(u)
            if w_.index in bset and w_.index not in seen: seen.add(w_.index); comp.append(w_.index); st.append(w_)
    cc = co[comp].mean(0)
    near = fab[np.linalg.norm(co[fab] - cc, axis=1) < 0.012]
    acc = {}
    for j in near:
        for k, x in W[j].items(): acc[k] = acc.get(k, 0) + x
    tot = sum(acc.values()) or 1
    for vi2 in comp: W[vi2] = {k: x/tot for k, x in acc.items()}
bm.free()
write_w(ch, W)
bpy.app.driver_namespace['w_chambray2'] = {'fabric': len(fabric), 'buttons': len(buttons)}
'''

S['w_trousers2'] = r'''
tr = bpy.data.objects['Arjun_Trousers']; me = tr.data
W = read_w(tr)
co = np.array([v.co[:] for v in me.vertices])
regf = np.empty(len(me.polygons), np.int32); me.attributes['companion_region'].data.foreach_get('value', regf)
ext = set()
for p in me.polygons:
    if regf[p.index] in (11, 12): ext.update(p.vertices)
loop = [i for i in range(len(co)) if abs(co[i,2] - 0.835) < 2e-4 and W[i] and not any(k == 'CC_Base_Waist' for k in W[i])]
lt = np.array([np.arctan2(co[i,0], -co[i,1]) for i in loop])
n = 0
for i in ext:
    if abs(co[i,2] - 0.835) < 1e-4 and W[i]: continue
    t = np.arctan2(co[i,0], -co[i,1])
    j = loop[int(np.argmin(np.abs((lt - t + np.pi) % (2*np.pi) - np.pi)))]
    s = ss(0.835, 0.959, co[i,2])
    W[i] = blend(W[j], WBAND, s); n += 1
write_w(tr, W)
bpy.app.driver_namespace['w_trousers'] = {'ext_verts': n, 'loop_verts': len(loop)}
'''

S['w_tee'] = r'''
tee = bpy.data.objects['Arjun_Tee_tmp']; me = tee.data
shirt = bpy.data.objects['Arjun_Shirt_Classic']; Ws = read_w(shirt); bs = tri_bvh(shirt)
body = bpy.data.objects['Arjun_Body']; Wb = read_w(body)
regb = np.empty(len(body.data.polygons), np.int32); body.data.attributes['companion_region'].data.foreach_get('value', regb)
skin_v = sorted({vi for p in body.data.polygons if regb[p.index] == 0 for vi in p.vertices if body.data.vertices[vi].co.z < 1.56})
kd = mathutils.kdtree.KDTree(len(skin_v))
for k, vi in enumerate(skin_v): kd.insert(body.data.vertices[vi].co, k)
kd.balance()
N = 144
W = []
nv = len(me.vertices); nrings = nv // N
for i, v in enumerate(me.vertices):
    p = v.co; k = i // N
    ws = strip(interp_w(shirt, Ws, bs, p), ('Shirt_',))
    if p.z <= 1.0:
        w = blend(WBAND, ws, ss(0.997, 1.06, p.z))
    else:
        w = blend(WBAND, ws, ss(0.997, 1.06, p.z))
    # neckline: the top rings follow the neck skin
    top = nrings - 4
    if k >= top - 6:
        co_, idx, d = kd.find(p)
        wk = Wb[skin_v[idx]]
        t = 1.0 if k >= top else ((k - (top - 6))/6.0)**2
        w = blend(w, wk, t)
    W.append(w)
write_w(tee, W)
bpy.app.driver_namespace['w_tee'] = {'verts': nv, 'rings': nrings}
'''

S['w_small'] = r'''
ch = bpy.data.objects['Arjun_Shirt_Chambray']; Wc = read_w(ch)
cco = np.array([v.co[:] for v in ch.data.vertices])
bt = bpy.data.objects['Arjun_Buttons_tmp']; bme = bt.data
# rigid per button: average weights of the shirt verts under it
bm = bmesh.new(); bm.from_mesh(bme); bm.verts.ensure_lookup_table()
comps = []; seen = set()
for v in bm.verts:
    if v.index in seen: continue
    comp = [v.index]; seen.add(v.index); st = [v]
    while st:
        u = st.pop()
        for e in u.link_edges:
            w_ = e.other_vert(u)
            if w_.index not in seen: seen.add(w_.index); comp.append(w_.index); st.append(w_)
    comps.append(comp)
bm.free()
W = [dict() for _ in range(len(bme.vertices))]
for comp in comps:
    cc = np.mean([bme.vertices[i].co[:] for i in comp], axis=0)
    near = np.nonzero(np.linalg.norm(cco - cc, axis=1) < 0.012)[0]
    acc = {}
    for j in near:
        for k, x in Wc[j].items(): acc[k] = acc.get(k, 0) + x
    tot = sum(acc.values()) or 1
    acc = {k: x/tot for k, x in acc.items()}
    for i in comp: W[i] = acc
write_w(bt, W)
wt = bpy.data.objects['Arjun_Watch']
write_w(wt, [{'CC_Base_L_Forearm': 0.93, 'CC_Base_L_Hand': 0.07} for _ in wt.data.vertices])
for ob in (bt, wt, bpy.data.objects['Arjun_Tee_tmp']):
    mod = next((m for m in ob.modifiers if m.type == 'ARMATURE'), None) or ob.modifiers.new('Armature', 'ARMATURE')
    mod.object = bpy.data.objects['Arjun_Rig']
bpy.app.driver_namespace['w_small'] = {'buttons': len(comps)}
'''

S['weights_fix'] = r'''
import numpy as np, bmesh
from mathutils.kdtree import KDTree
rig = bpy.data.objects['Arjun_Rig']
deform = {b.name for b in rig.data.bones if b.use_deform}
allowed_for = {
    'Arjun_Shirt_Classic': lambda n: n.startswith('CC_Base') or n.startswith('Shirt_') or n.startswith('Share_'),
    'Arjun_Shirt_Chambray': lambda n: n.startswith('CC_Base') or n.startswith('Chambray_') or n.startswith('Share_'),
    'Arjun_Trousers': lambda n: n.startswith('CC_Base'),
    'Arjun_Sneakers': lambda n: n.startswith('CC_Base'),
    'Arjun_Watch': lambda n: n.startswith('CC_Base'),
}
report = {}
for name, ok in allowed_for.items():
    ob = bpy.data.objects[name]; me = ob.data
    gname = {g.index: g.name for g in ob.vertex_groups}
    W = []
    for v in me.vertices:
        w = {gname[g.group]: g.weight for g in v.groups if g.weight > 0 and gname[g.group] in deform and ok(gname[g.group])}
        W.append(w)
    good = [i for i, w in enumerate(W) if sum(w.values()) > 1e-4]
    bad = [i for i, w in enumerate(W) if sum(w.values()) <= 1e-4]
    if bad:
        kd = KDTree(len(good))
        for k, i in enumerate(good): kd.insert(me.vertices[i].co, k)
        kd.balance()
        for i in bad:
            near = kd.find_n(me.vertices[i].co, 4)
            acc = {}
            for co, k, d in near:
                for b, x in W[good[k]].items(): acc[b] = acc.get(b, 0) + x/(d + 1e-3)
            W[i] = acc
    # rewrite groups (limit 4, normalise)
    for g in list(ob.vertex_groups): ob.vertex_groups.remove(g)
    groups = {}
    for i, w in enumerate(W):
        items = sorted(w.items(), key=lambda kv: -kv[1])[:4]; tot = sum(x for _, x in items)
        for b, x in items:
            x = x/tot
            if x < 0.01: continue
            if b not in groups: groups[b] = ob.vertex_groups.new(name=b)
            groups[b].add([i], round(x, 3), 'REPLACE')
    report[name] = {'refilled': len(bad), 'groups': len(groups)}
# triangulate n-gons in the chambray (holes_fill faces) so no polygon is discarded on import
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ch.data)
ngons = [f for f in bm.faces if len(f.verts) > 4]
bmesh.ops.triangulate(bm, faces=ngons)
bm.to_mesh(ch.data); bm.free(); ch.data.update()
report['ngons'] = len(ngons)
bpy.app.driver_namespace['weights_fix'] = report
'''

# ==================================================================================================
# ---- 13h Merge the tee and buttons into the top, export mesh data, load the painted atlases ----
# Texture layouts and paint run outside Blender: ../textures/uvpack.py then paint_top.py, paint_trousers.py,
# paint_watch.py (python -I, numpy + PIL + scipy).
S['merge_top'] = r'''
import numpy as np
ch = bpy.data.objects['Arjun_Shirt_Chambray']; tee = bpy.data.objects['Arjun_Tee_tmp']; bt = bpy.data.objects['Arjun_Buttons_tmp']
def mat(name, col):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.diffuse_color = col; return m
m_top = mat('Arjun_Chambray_Top', (0.45, 0.58, 0.72, 1)); m_tee = mat('Arjun_Tee', (0.92, 0.91, 0.89, 1)); m_btn = mat('Arjun_Chambray_Buttons', (0.95, 0.95, 0.93, 1))
# shirt: one fabric slot (flattened trims are fabric now)
me = ch.data
idx = np.zeros(len(me.polygons), np.int32); me.polygons.foreach_set('material_index', idx)
me.materials.clear(); me.materials.append(m_top)
for ob, m, rid in ((tee, m_tee, 20), (bt, m_btn, 21)):
    ob.data.materials.clear(); ob.data.materials.append(m)
    a = ob.data.attributes.get('companion_region') or ob.data.attributes.new('companion_region', 'INT', 'FACE')
    a.data.foreach_set('value', np.full(len(ob.data.polygons), rid, np.int32))
for ob in (ch, tee, bt):
    ob.data.uv_layers[0].name = 'UVOld'
for o in bpy.context.view_layer.objects: o.select_set(False)
for o in (ch, tee, bt): o.select_set(True)
with bpy.context.temp_override(active_object=ch, object=ch, selected_objects=[ch, tee, bt], selected_editable_objects=[ch, tee, bt]):
    bpy.ops.object.join()
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bpy.app.driver_namespace['merge_top'] = {'v': len(ch.data.vertices), 'mats': [m.name for m in ch.data.materials], 'uv': [u.name for u in ch.data.uv_layers]}
'''

S['export_npz'] = r'''
import numpy as np, os
OUT = 'C:/Users/user/AppData/Local/Temp/claude/D--Unity-ai-companion/458c8edc-75e0-47b5-9763-dd6165fe2b2f/scratchpad/outfit/mesh'
os.makedirs(OUT, exist_ok=True)
for name in ('Arjun_Shirt_Chambray', 'Arjun_Trousers', 'Arjun_Watch'):
    ob = bpy.data.objects[name]; me = ob.data
    me.calc_loop_triangles()
    nv = len(me.vertices); nl = len(me.loops); nf = len(me.polygons)
    co = np.empty(nv*3, np.float32); me.vertices.foreach_get('co', co)
    vn = np.empty(nv*3, np.float32); me.vertex_normals.foreach_get('vector', vn)
    lv = np.empty(nl, np.int32); me.loops.foreach_get('vertex_index', lv)
    uv = np.empty(nl*2, np.float32); me.uv_layers['UVOld' if 'UVOld' in me.uv_layers else 0].data.foreach_get('uv', uv)
    ls = np.empty(nf, np.int32); me.polygons.foreach_get('loop_start', ls)
    lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
    fm = np.empty(nf, np.int32); me.polygons.foreach_get('material_index', fm)
    reg = np.zeros(nf, np.int32)
    if 'companion_region' in me.attributes: me.attributes['companion_region'].data.foreach_get('value', reg)
    nt = len(me.loop_triangles)
    tl = np.empty(nt*3, np.int32); me.loop_triangles.foreach_get('loops', tl)
    tp = np.empty(nt, np.int32); me.loop_triangles.foreach_get('polygon_index', tp)
    np.savez_compressed(f'{OUT}/{name}.npz', co=co.reshape(-1,3), vn=vn.reshape(-1,3), lv=lv, uv=uv.reshape(-1,2), ls=ls, lt=lt, fm=fm, reg=reg, tl=tl.reshape(-1,3), tp=tp, mats=np.array([m.name for m in me.materials]))
bpy.app.driver_namespace['export_npz'] = OUT
'''

S['load_top_tex'] = r'''
import numpy as np
O = 'C:/Users/user/AppData/Local/Temp/claude/D--Unity-ai-companion/458c8edc-75e0-47b5-9763-dd6165fe2b2f/scratchpad/outfit'
def set_uv(ob, path):
    me = ob.data
    uv = np.load(path).astype(np.float32)
    lay = me.uv_layers.get('UVOutfit') or me.uv_layers.new(name='UVOutfit')
    lay.data.foreach_set('uv', uv.ravel())
    me.uv_layers.active = lay
    lay.active_render = True
def img(name, path, colorspace):
    im = bpy.data.images.get(name)
    if im is None:
        im = bpy.data.images.load(path); im.name = name
    else:
        im.filepath = path; im.reload()
    im.colorspace_settings.name = colorspace
    return im
def review_mat(name, base, normal, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    uvn = nt.nodes.new('ShaderNodeUVMap'); uvn.uv_map = 'UVOutfit'
    tb = nt.nodes.new('ShaderNodeTexImage'); tb.image = base
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = normal
    nm = nt.nodes.new('ShaderNodeNormalMap'); nm.uv_map = 'UVOutfit'
    nt.links.new(uvn.outputs['UV'], tb.inputs['Vector']); nt.links.new(uvn.outputs['UV'], tn.inputs['Vector'])
    nt.links.new(tb.outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(tn.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    bsdf.inputs['Roughness'].default_value = rough
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    nt.nodes.active = tb
    return m
ch = bpy.data.objects['Arjun_Shirt_Chambray']
set_uv(ch, O + '/uv/Arjun_Shirt_Chambray_uv.npy')
b = img('Arjun_Chambray_BaseColor', O + '/tex/Arjun_Chambray_BaseColor.png', 'sRGB')
n = img('Arjun_Chambray_Normal', O + '/tex/Arjun_Chambray_Normal.png', 'Non-Color')
for name, rough in (('Arjun_Chambray_Top', 0.78), ('Arjun_Tee', 0.82), ('Arjun_Chambray_Buttons', 0.4)):
    review_mat(name, b, n, rough)
'''

S['watch_tr_tex'] = r'''
import numpy as np
O = 'C:/Users/user/AppData/Local/Temp/claude/D--Unity-ai-companion/458c8edc-75e0-47b5-9763-dd6165fe2b2f/scratchpad/outfit'
def img(name, path, colorspace):
    im = bpy.data.images.get(name)
    if im is None:
        im = bpy.data.images.load(path); im.name = name
    else:
        im.filepath = path; im.reload()
    im.colorspace_settings.name = colorspace
    return im
def review_mat(name, base, normal, rough, uvname, metal=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    uvn = nt.nodes.new('ShaderNodeUVMap'); uvn.uv_map = uvname
    tb = nt.nodes.new('ShaderNodeTexImage'); tb.image = base
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = normal
    nm = nt.nodes.new('ShaderNodeNormalMap'); nm.uv_map = uvname
    nt.links.new(uvn.outputs['UV'], tb.inputs['Vector']); nt.links.new(uvn.outputs['UV'], tn.inputs['Vector'])
    nt.links.new(tb.outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(tn.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    bsdf.inputs['Roughness'].default_value = rough; bsdf.inputs['Metallic'].default_value = metal
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    nt.nodes.active = tb
    return m
# watch: mirror the dial mapping (12 o'clock must face away from the crown side correctly) and name the layer
wt = bpy.data.objects['Arjun_Watch']; me = wt.data
lay = me.uv_layers[0]; lay.name = 'UVOutfit'
uv = np.empty(len(me.loops)*2, np.float32); lay.data.foreach_get('uv', uv); uv = uv.reshape(-1,2)
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
for p in me.polygons:
    if fm[p.index] == 1:
        for li in p.loop_indices: uv[li,1] = 1.0 - uv[li,1]
lay.data.foreach_set('uv', uv.ravel())
for p in me.polygons:
    if fm[p.index] == 1: p.use_smooth = False
wb = img('Arjun_Watch_BaseColor', O + '/tex/Arjun_Watch_BaseColor.png', 'sRGB'); wn = img('Arjun_Watch_Normal', O + '/tex/Arjun_Watch_Normal.png', 'Non-Color')
review_mat('Arjun_Watch_Steel', wb, wn, 0.28, 'UVOutfit', 0.9)
review_mat('Arjun_Watch_Dial', wb, wn, 0.12, 'UVOutfit', 0.0)
# trousers: new layout + material named for export
tr = bpy.data.objects['Arjun_Trousers']; me = tr.data
uvn = np.load(O + '/uv/Arjun_Trousers_uv.npy').astype(np.float32)
lay = me.uv_layers.get('UVOutfit') or me.uv_layers.new(name='UVOutfit')
lay.data.foreach_set('uv', uvn.ravel()); me.uv_layers.active = lay; lay.active_render = True
tb = img('Arjun_Chinos_Olive_BaseColor', O + '/tex/Arjun_Chinos_Olive_BaseColor.png', 'sRGB')
gb = img('Arjun_Trousers_Grey_BaseColor', O + '/tex/Arjun_Trousers_Grey_BaseColor.png', 'sRGB')
tn = img('Arjun_Trousers_Normal', O + '/tex/Arjun_Trousers_Normal.png', 'Non-Color')
m_tr = review_mat('Arjun_Trousers_Bottom', tb, tn, 0.85, 'UVOutfit')
me.materials.clear(); me.materials.append(m_tr)
'''

# ==================================================================================================
# ---- 13i Export the base body and one FBX per garment ----
# Deform flags select the bones per file: base = CC_Base + hair, each garment = CC_Base + its own chain bones.
# The garment chain files (<Garment>.item.json) use the rig.json chain format. Triangulate n-gons first
# (Unity discards self-intersecting polygons).
S['exp_prepare'] = r'''
import numpy as np
rig = bpy.data.objects['Arjun_Rig']
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1,0,0,0); pb.location = (0,0,0); pb.scale = (1,1,1)
for name, cname in bpy.app.driver_namespace.get('muted_constraints', []):
    c = rig.pose.bones[name].constraints.get(cname)
    if c: c.mute = False
bpy.app.driver_namespace['muted_constraints'] = []
for n in ('Arjun_Shirt_Chambray', 'Arjun_Trousers'):
    me = bpy.data.objects[n].data
    if 'UVOld' in me.uv_layers:
        uv = np.empty(len(me.loops)*2, np.float32); me.uv_layers['UVOld'].data.foreach_get('uv', uv); uv = uv.reshape(-1,2)
        a = me.attributes.get('uvold3') or me.attributes.new('uvold3', 'FLOAT_VECTOR', 'CORNER')
        a.data.foreach_set('vector', np.hstack([uv, np.zeros((len(uv),1), np.float32)]).ravel())
        me.uv_layers.remove(me.uv_layers['UVOld'])
    me.uv_layers['UVOutfit'].active = True; me.uv_layers['UVOutfit'].active_render = True
for ob in bpy.data.objects:
    if ob.type == 'MESH':
        for ca in list(ob.data.color_attributes):
            if ca.name == 'dbg': ob.data.color_attributes.remove(ca)
        if ob.data.shape_keys:
            for kb in ob.data.shape_keys.key_blocks: kb.value = 0.0
bpy.context.view_layer.update()
bpy.app.driver_namespace['exp_prepare'] = {o.name: [u.name for u in o.data.uv_layers] for o in bpy.data.objects if o.type == 'MESH'}
'''

S['exp_json'] = r'''
import json, os, numpy as np
OUTD = 'D:/Blender/Companion_Outfits_20261009_Arjun/export'
os.makedirs(OUTD + '/Wardrobe', exist_ok=True)
rig = bpy.data.objects['Arjun_Rig']; B = {b.name: b for b in rig.data.bones}
old = json.load(open('D:/Unity/ai_companion/apps/unity/Assets/Companion/Imported/Arjun/Arjun.rig.json'))
cols = old['colliders']
def chain(name, bones, stiff, drag, grav, rad):
    return {'name': name, 'bones': bones, 'tip': list(map(float, B[bones[-1]].tail_local)), 'stiffness': stiff, 'drag': drag, 'gravity': grav, 'radius': rad}
def heads(pred):
    return [{'name': n, 'head': list(map(float, b.head_local))} for n, b in sorted(B.items()) if pred(n, b)]
garment = lambda n: n.startswith(('Shirt_', 'Chambray_', 'Share_'))
hair_chains = [c for c in old['chains'] if c['name'].startswith('Hair_')]
base = {'source': bpy.data.filepath, 'space': 'blender_z_up_meters', 'bones': heads(lambda n, b: b.use_deform and not garment(n)), 'chains': hair_chains, 'colliders': cols}
json.dump(base, open(OUTD + '/Arjun.rig.json', 'w'), indent=1)
classic = [c for c in old['chains'] if c['name'].startswith('Shirt_')]
ch_chains = [chain(f'Chambray_{k}', [f'Chambray_{k}_01', f'Chambray_{k}_02', f'Chambray_{k}_03'], 1.1, 0.5, 0.15, 0.02) for k in ('FR', 'FL')]
ch_chains += [chain(f'Chambray_{k}', [f'Chambray_{k}_01', f'Chambray_{k}_02'], 0.9, 0.45, 0.15, 0.02) for k in ('L', 'BL', 'B', 'BR', 'R')]
# share joints (13j): helper bones the app turns by a fraction of a body bone (CompanionSecondaryMotion.shares)
shares = [{'bone': f'Share_{s}_Upperarm', 'source': f'CC_Base_{s}_Upperarm', 'amount': 0.5} for s in ('L', 'R')]
for name, chains, prefix in (('Arjun_Shirt_Classic', classic, 'Shirt_'), ('Arjun_Shirt_Chambray', ch_chains, 'Chambray_')):
    item = {'source': bpy.data.filepath, 'space': 'blender_z_up_meters', 'bones': heads(lambda n, b, p=prefix: b.use_deform and (n.startswith('CC_Base') or n.startswith(p) or n.startswith('Share_'))), 'chains': chains, 'shares': shares}
    json.dump(item, open(OUTD + f'/Wardrobe/{name}.item.json', 'w'), indent=1)
# rest-pose clearance of every garment joint against the body colliders
def seg_dist(c, p):
    a = np.array(c['center']); b = np.array(c['tail']); d = b - a
    t = 0.0 if d @ d < 1e-12 else np.clip(((p - a) @ d)/(d @ d), 0, 1)
    return np.linalg.norm(p - (a + t*d)) - c['radius']
viol = []
for ch in classic + ch_chains:
    pts = [np.array(B[n].head_local) for n in ch['bones'][1:]] + [np.array(ch['tip'])]
    for i, p in enumerate(pts):
        for c in cols:
            d = seg_dist(c, p) - ch['radius']
            if d < 0.002: viol.append((ch['name'], i, c['bone'], round(float(d)*1000, 1)))
bpy.app.driver_namespace['exp_json'] = {'base_bones': len(base['bones']), 'hair_chains': len(hair_chains), 'classic': len(classic), 'chambray': len(ch_chains), 'violations': viol}
'''

S['exp_fbx'] = r'''
OUTD = 'D:/Blender/Companion_Outfits_20261009_Arjun/export'
rig = bpy.data.objects['Arjun_Rig']
saved = {b.name: b.use_deform for b in rig.data.bones}
def export(path, meshes, keep):
    for b in rig.data.bones:
        b.use_deform = saved[b.name] and keep(b.name)
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects: o.select_set(False)
    objs = [rig] + [bpy.data.objects[n] for n in meshes]
    for o in objs: o.hide_set(False); o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=False, mesh_smooth_type='OFF', use_tspace=False, colors_type='NONE',
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=True,
        armature_nodetype='NULL', bake_anim=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=False, path_mode='STRIP', embed_textures=False)
core = lambda n: n.startswith('CC_Base')
try:
    export(OUTD + '/Arjun.fbx', ('Arjun_Body', 'Arjun_Eyes', 'Arjun_Mouth'), lambda n: core(n) or n.startswith('Hair_'))
    export(OUTD + '/Wardrobe/Arjun_Shirt_Classic.fbx', ('Arjun_Shirt_Classic',), lambda n: core(n) or n.startswith('Shirt_') or n.startswith('Share_'))
    export(OUTD + '/Wardrobe/Arjun_Shirt_Chambray.fbx', ('Arjun_Shirt_Chambray',), lambda n: core(n) or n.startswith('Chambray_') or n.startswith('Share_'))
    for n in ('Arjun_Trousers', 'Arjun_Sneakers', 'Arjun_Watch'):
        export(OUTD + f'/Wardrobe/{n}.fbx', (n,), core)
finally:
    for b in rig.data.bones: b.use_deform = saved[b.name]
    rig.data.pose_position = 'POSE'
import os
bpy.app.driver_namespace['exp_fbx'] = {f: os.path.getsize(os.path.join(dp, f)) for dp, dn, fn in os.walk(OUTD) for f in fn if f.endswith('.fbx')}
'''

S['exp_tex'] = r'''
import shutil, os
O = 'C:/Users/user/AppData/Local/Temp/claude/D--Unity-ai-companion/458c8edc-75e0-47b5-9763-dd6165fe2b2f/scratchpad/outfit/tex'
D = 'D:/Blender/Companion_Outfits_20261009_Arjun/export/textures'
os.makedirs(D, exist_ok=True)
names = ['Arjun_Chambray_BaseColor.png','Arjun_Chambray_Normal.png','Arjun_Trousers_Grey_BaseColor.png','Arjun_Chinos_Olive_BaseColor.png','Arjun_Trousers_Normal.png','Arjun_Watch_BaseColor.png','Arjun_Watch_Normal.png']
for n in names: shutil.copyfile(O + '/' + n, D + '/' + n)
bpy.app.driver_namespace['exp_tex'] = {n: os.path.getsize(D + '/' + n) for n in names}
'''

# ==================================================================================================
# ---- 13j Owner review fixes (2026-10-10): neck skin, armpits, tee neckline, collar edges ----
# The owner saw blue fabric on Arjun's neck and stretched armpits when he raised his arms. Checkpoint
# first. Run the blocks in this order, then re-export with 13i (deform flags and item.json include Share_).

# Neck skin was left inside the garments by the 13b colour rule ("luminance < 100 below z 1.52" also caught
# shadowed neck skin). The classic shirt kept the original texture there, so it looked like skin; the
# chambray copy repainted it blue. Select those faces by colour (seed: V > 0.36 and S > 0.42; grow through
# V > 0.22, S > 0.40, H < 0.12), keep only components touching the body's open boundary, fill faces with two
# selected neighbours. 'uvold3' holds the chambray's original atlas UVs.
NECK_SEL = r'''
import bpy, numpy as np, colorsys, bmesh
from mathutils import kdtree
def neck_skin_faces(ob, uv_kind):
    A = bpy.app.driver_namespace['atlas_u8']
    me = ob.data; nf = len(me.polygons)
    if uv_kind == 'uvold3':
        u3 = np.empty(len(me.loops)*3, np.float32); me.attributes['uvold3'].data.foreach_get('vector', u3); uv = u3.reshape(-1,3)[:,:2]
    else:
        uv = np.empty(len(me.loops)*2, np.float32); me.uv_layers[uv_kind].data.foreach_get('uv', uv); uv = uv.reshape(-1,2)
    lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
    cen = np.empty(nf*3, np.float32); me.polygons.foreach_get('center', cen); cen = cen.reshape(-1,3)
    mi = np.empty(nf, np.int32); me.polygons.foreach_get('material_index', mi)
    fidx = np.repeat(np.arange(nf), lt)
    su = np.zeros((nf,2)); np.add.at(su, fidx, uv); su /= lt[:,None]
    def samp(u):
        x = np.clip((u[:,0]%1.0)*4096, 0, 4095).astype(int); y = np.clip((u[:,1]%1.0)*4096, 0, 4095).astype(int)
        return A[y, x].astype(np.float32)
    cc = np.zeros((nf,3)); np.add.at(cc, fidx, samp(uv)); cc /= lt[:,None]
    col = 0.5*samp(su) + 0.5*cc
    hsv = np.array([colorsys.rgb_to_hsv(*(c/255.0)) for c in col]); H, S, V = hsv[:,0], hsv[:,1], hsv[:,2]
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
    adj = [[] for _ in range(nf)]
    for e in bm.edges:
        fs = [f.index for f in e.link_faces]
        for i in fs:
            for j in fs:
                if i != j: adj[i].append(j)
    bverts = set(v.index for e in bm.edges if e.is_boundary for v in e.verts)
    body = bpy.data.objects['Arjun_Body']; bme = bmesh.new(); bme.from_mesh(body.data)
    bb = np.array([v.co[:] for e in bme.edges if e.is_boundary for v in e.verts]); bme.free()
    kd = kdtree.KDTree(len(bb))
    for i, p in enumerate(bb): kd.insert(p, i)
    kd.balance()
    shared = set(i for i in bverts if kd.find(bm.verts[i].co)[2] < 1e-5)
    touch = np.array([any(v.index in shared for v in f.verts) for f in bm.faces])
    bm.free()
    zone = (cen[:,2] > 1.36) & (np.abs(cen[:,0]) < 0.13) & (mi == 0)
    seed = (V > 0.36) & (S > 0.42) & zone
    loose = (V > 0.22) & (S > 0.40) & (H < 0.12) & zone
    reg = seed.copy(); fr = list(np.nonzero(reg)[0])
    while fr:
        nx = []
        for f in fr:
            for g in adj[f]:
                if not reg[g] and loose[g]: reg[g] = True; nx.append(g)
        fr = nx
    comp = -np.ones(nf, int); keep = np.zeros(nf, bool); cid = 0
    for f in np.nonzero(reg)[0]:
        if comp[f] >= 0: continue
        st = [f]; comp[f] = cid; mem = [f]
        while st:
            x = st.pop()
            for g in adj[x]:
                if reg[g] and comp[g] < 0: comp[g] = cid; st.append(g); mem.append(g)
        if touch[mem].any() and len(mem) >= 3: keep[mem] = True
        cid += 1
    for it in range(3):
        add = [f for f in range(nf) if not keep[f] and zone[f] and adj[f] and sum(keep[g] for g in adj[f]) >= 2]
        for f in add: keep[f] = True
    return keep, cen
'''
S['neck_select'] = NECK_SEL + r'''
kc, cc = neck_skin_faces(bpy.data.objects['Arjun_Shirt_Classic'], 'UVMap')
kh, ch = neck_skin_faces(bpy.data.objects['Arjun_Shirt_Chambray'], 'uvold3')
ns = bpy.app.driver_namespace
ns['neck_keep_classic'] = kc; ns['neck_keep_chambray'] = kh; ns['neck_classic_centroids'] = cc[kc]
ns['neck_info'] = {'classic': int(kc.sum()), 'chambray': int(kh.sum())}
'''

# The patch comes from the pre-split checkpoint, so it keeps the original weights, face-shape deltas (the
# ARKit jaw shapes move the under-chin skin) and vertex normals ('orig_normal'). Arjun: 668 faces, 420 verts.
S['neck_patch'] = r'''
import bpy, bmesh, numpy as np
from mathutils import kdtree
ns = bpy.app.driver_namespace
P = 'D:/Blender/Companion_Outfits_20261009_Arjun/checkpoints/arjun-outfits-before-split.blend'
mats0 = set(bpy.data.materials.keys()); imgs0 = set(bpy.data.images.keys())
with bpy.data.libraries.load(P, link=False) as (src, dst):
    dst.meshes = ['Arjun_Body']
me = dst.meshes[0]; me.name = 'NeckPatch'
vn = np.array([v.normal[:] for v in me.vertices], np.float32)
a = me.attributes.get('orig_normal') or me.attributes.new('orig_normal', 'FLOAT_VECTOR', 'POINT')
a.data.foreach_set('vector', vn.ravel())
nf = len(me.polygons)
cen = np.empty(nf*3, np.float32); me.polygons.foreach_get('center', cen); cen = cen.reshape(-1,3)
kd = kdtree.KDTree(nf)
for i, p in enumerate(cen): kd.insert(p, i)
kd.balance()
match = []; miss = 0
for p in ns['neck_classic_centroids']:
    co, i, d = kd.find(p)
    if d < 1e-5: match.append(i)
    else: miss += 1
match = set(match)
ob = bpy.data.objects.new('NeckPatch', me)
bpy.data.collections['Companion_Work'].objects.link(ob)
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in match], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(me); bm.free()
me.materials.clear(); me.materials.append(bpy.data.materials['Arjun_Skin'])
me.polygons.foreach_set('material_index', np.zeros(len(me.polygons), np.int32))
reg = me.attributes.get('companion_region')
if reg is not None: reg.data.foreach_set('value', np.zeros(len(me.polygons), np.int32))
me.update()
for m in list(bpy.data.materials):
    if m.name not in mats0 and m.users == 0: bpy.data.materials.remove(m)
for im in list(bpy.data.images):
    if im.name not in imgs0 and im.users == 0: bpy.data.images.remove(im)
ns['neck_patch'] = {'matched': len(match), 'missed': miss, 'verts': len(me.vertices), 'faces': len(me.polygons)}
'''

# Join keeps the body's shape keys (matched by name); weld with an explicit map so the body's seam verts
# (and their face-shape deltas) survive; custom normals again from 'orig_normal'.
S['neck_join'] = r'''
import bpy, bmesh, numpy as np
from mathutils import kdtree
ns = bpy.app.driver_namespace
body = bpy.data.objects['Arjun_Body']; patch = bpy.data.objects['NeckPatch']
groups0 = [g.name for g in body.vertex_groups]
nb = len(body.data.vertices)
patch.parent = body.parent; patch.matrix_world = body.matrix_world.copy()
for o in bpy.context.view_layer.objects: o.select_set(False)
body.select_set(True); patch.select_set(True)
with bpy.context.temp_override(active_object=body, object=body, selected_objects=[body, patch], selected_editable_objects=[body, patch]):
    bpy.ops.object.join()
body = bpy.data.objects['Arjun_Body']; me = body.data
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
old_b = [v for v in bm.verts if v.index < nb and v.is_boundary]
kd = kdtree.KDTree(len(old_b))
for i, v in enumerate(old_b): kd.insert(v.co, i)
kd.balance()
tmap = {}
for v in bm.verts:
    if v.index >= nb and v.is_boundary:
        co, i, d = kd.find(v.co)
        if d < 1e-6: tmap[v] = old_b[i]
bmesh.ops.weld_verts(bm, targetmap=tmap)
bm.to_mesh(me); bm.free(); me.update()
for n in [g.name for g in body.vertex_groups if g.name not in groups0]: body.vertex_groups.remove(body.vertex_groups[n])
vn = np.empty(len(me.vertices)*3, np.float32); me.attributes['orig_normal'].data.foreach_get('vector', vn)
vn = vn.reshape(-1,3); vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-9)[:,None]
for p in me.polygons: p.use_smooth = True
me.normals_split_custom_set_from_vertices([tuple(v) for v in vn]); me.update()
ns['neck_join'] = {'welded': len(tmap), 'verts': len(me.vertices)}
'''

# Delete the same faces from both shirts. The chambray keeps its hand-set corner normals through the edit.
S['neck_remove'] = r'''
import bpy, bmesh, numpy as np
ns = bpy.app.driver_namespace
out = {}
for name, key, keep_normals in (('Arjun_Shirt_Classic', 'neck_keep_classic', False), ('Arjun_Shirt_Chambray', 'neck_keep_chambray', True)):
    ob = bpy.data.objects[name]; me = ob.data
    kill = set(np.nonzero(ns[key])[0].tolist())
    if keep_normals:
        cn = np.array([l.vector[:] for l in me.corner_normals], np.float32)
        a = me.attributes.get('tmp_cn') or me.attributes.new('tmp_cn', 'FLOAT_VECTOR', 'CORNER')
        a.data.foreach_set('vector', cn.ravel())
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in sorted(kill)], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(me); bm.free(); me.update()
    if keep_normals:
        cn = np.empty(len(me.loops)*3, np.float32); me.attributes['tmp_cn'].data.foreach_get('vector', cn)
        me.normals_split_custom_set(cn.reshape(-1,3).tolist()); me.attributes.remove(me.attributes['tmp_cn']); me.update()
    else:
        vn = np.empty(len(me.vertices)*3, np.float32); me.attributes['orig_normal'].data.foreach_get('vector', vn)
        vn = vn.reshape(-1,3); vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-9)[:,None]
        for p in me.polygons: p.use_smooth = True
        me.normals_split_custom_set_from_vertices([tuple(v) for v in vn]); me.update()
    out[name] = {'faces_removed': len(kill), 'verts': len(me.vertices)}
ns['neck_remove'] = out
'''

# Armpits. The T-pose shirt has a 5 cm underarm fold (8-10 cm at the front and back), and the inherited
# weights gave the side panel 0.3-0.6 upper-arm weight 10-15 cm below the armpit. Raised arms dragged the
# whole side into a web from elbow to hem. Fix: one half-rotation share bone per shoulder (child of the
# clavicle, same head, tail and roll as the upper arm; the app turns it with CompanionSecondaryMotion.shares)
# and new underarm weights. The Copy Rotation constraint (local, 0.5) is a Blender preview of the app driver.
S['share_bones'] = r'''
import bpy
rig = bpy.data.objects['Arjun_Rig']
for o in bpy.context.view_layer.objects: o.select_set(False)
rig.select_set(True); bpy.context.view_layer.objects.active = rig
with bpy.context.temp_override(active_object=rig, object=rig):
    bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones
for s in ('L', 'R'):
    up = eb[f'CC_Base_{s}_Upperarm']
    b = eb.get(f'Share_{s}_Upperarm') or eb.new(f'Share_{s}_Upperarm')
    b.head = up.head.copy(); b.tail = up.tail.copy(); b.roll = up.roll
    b.parent = eb[f'CC_Base_{s}_Clavicle']; b.use_connect = False; b.use_deform = True
with bpy.context.temp_override(active_object=rig, object=rig):
    bpy.ops.object.mode_set(mode='OBJECT')
coll = rig.data.collections.get('Deform')
for s in ('L', 'R'):
    if coll: coll.assign(rig.data.bones[f'Share_{s}_Upperarm'])
    pb = rig.pose.bones[f'Share_{s}_Upperarm']
    c = pb.constraints.get('Half upper arm') or pb.constraints.new('COPY_ROTATION')
    c.name = 'Half upper arm'; c.target = rig; c.subtarget = f'CC_Base_{s}_Upperarm'
    c.owner_space = 'LOCAL'; c.target_space = 'LOCAL'; c.mix_mode = 'REPLACE'; c.influence = 0.5
'''

# Underarm weights in arm-aligned coordinates (t along the arm from the shoulder joint, r from the arm axis,
# phi around it, 0 = straight down). Per phi: sleeve radius R from t 0.07-0.10, torso wall TW from points
# well below the tube. s = X/(X+Y) with X = t - TW (lateral of the wall), Y = r - R (below the tube) runs
# 0 on the side panel to 1 on the sleeve: s < 0.2 chest, 0.2-0.5 chest -> share, 0.5-0.8 share -> arm,
# > 0.8 arm. Only the lower half (|phi| < 115) near the armpit changes; 40 Laplacian passes smooth the field
# (unsmoothed weights crease the front fold and pinch the hanging arm). Limit 4 influences.
ARMPIT_LIB = r'''
import bpy, numpy as np
def ss(x):
    x = np.clip(x, 0, 1); return x*x*(3-2*x)
def read_matrix(ob):
    me = ob.data; names = [g.name for g in ob.vertex_groups]
    W = np.zeros((len(me.vertices), len(names)))
    for v in me.vertices:
        for g in v.groups: W[v.index, g.group] = g.weight
    co = np.empty(len(me.vertices)*3); me.vertices.foreach_get('co', co)
    return names, W, co.reshape(-1,3)
def vert_mat(me):
    nf = len(me.polygons); lt = np.empty(nf, np.int32); me.polygons.foreach_get('loop_total', lt)
    li = np.empty(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', li)
    mi = np.empty(nf, np.int32); me.polygons.foreach_get('material_index', mi)
    vm = np.full(len(me.vertices), -1); vm[li] = mi[np.repeat(np.arange(nf), lt)]
    return vm
def arm_frame(rig, co, side):
    b = rig.data.bones[f'CC_Base_{side}_Upperarm']
    J = np.array(b.head_local); A = np.array(b.tail_local) - J; A /= np.linalg.norm(A)
    p = co - J; t = p @ A; rv = p - np.outer(t, A); r = np.linalg.norm(rv, axis=1)
    down = np.array([0, 0, -1.0]); down -= (down @ A)*A; down /= np.linalg.norm(down)
    back = np.cross(A, down)
    if back[1] < 0: back = -back
    return t, r, np.degrees(np.arctan2(rv @ back, rv @ down))
def per_phi(bins, vals, phi):
    c = 0.5*(bins[:-1] + bins[1:]); ok = ~np.isnan(vals)
    cc = np.concatenate([c[ok]-360, c[ok], c[ok]+360]); vv = np.tile(vals[ok], 3)
    return np.interp(phi, cc, vv)
def smooth_field(F, ev, deg, mask, iters, alpha=0.5):
    F = F.copy()
    for _ in range(iters):
        acc = np.zeros_like(F)
        np.add.at(acc, ev[:,0], F[ev[:,1]]); np.add.at(acc, ev[:,1], F[ev[:,0]])
        F += (alpha*mask)[:,None]*(acc/np.maximum(deg, 1)[:,None] - F)
    return F
def armpit_weights(ob, rig, lo=0.2, hi=0.8, trange=(-0.07, 0.15), phimax=115.0, iters=40):
    names, W, co = read_matrix(ob)
    vm = vert_mat(ob.data); fabric = vm == 0
    out_names = list(names)
    for side in ('L', 'R'):
        if f'Share_{side}_Upperarm' not in out_names: out_names.append(f'Share_{side}_Upperarm')
    W1 = np.zeros((len(co), len(out_names))); W1[:, :len(names)] = W
    bins = np.arange(-180, 181, 10)
    for side in ('L', 'R'):
        t, r, phi = arm_frame(rig, co, side)
        R = np.full(len(bins)-1, np.nan); TW = np.full(len(bins)-1, np.nan)
        for i in range(len(bins)-1):
            m = fabric & (phi >= bins[i]) & (phi < bins[i+1])
            s = m & (t > 0.07) & (t < 0.10)
            if s.sum() > 3: R[i] = np.median(r[s])
        R = np.where(np.isnan(R), np.nanmedian(R), R)
        for i in range(len(bins)-1):
            m = fabric & (phi >= bins[i]) & (phi < bins[i+1]) & (r > R[i] + 0.06) & (t > -0.08) & (t < 0.10)
            if m.sum() > 3: TW[i] = np.percentile(t[m], 90)
        TW = np.where(np.isnan(TW), np.nanmax(TW), TW)
        X = t - per_phi(bins, TW, phi); Y = r - per_phi(bins, R, phi)
        s = np.where(Y <= 0, 1.0, np.where(X <= 0, 0.0, X/np.maximum(X + Y, 1e-9)))
        w_arm = ss((s - 0.5)/(hi - 0.5)); w_tor = ss((0.5 - s)/(0.5 - lo)); w_sh = 1 - w_arm - w_tor
        m = ss((phimax - np.abs(phi))/25.0)*ss((t - trange[0])/0.03)*ss((trange[1] - t)/0.03)*ss((per_phi(bins, R, phi) + 0.20 - r)/0.04)
        m *= (co[:, 0]*(1 if side == 'L' else -1)) > 0.02
        iu = names.index(f'CC_Base_{side}_Upperarm'); armset = [iu] + ([names.index(f'CC_Base_{side}_Forearm')] if f'CC_Base_{side}_Forearm' in names else [])
        isarm = np.isin(np.arange(len(names)), armset)
        orig_arm = W[:, armset].sum(1)
        nonarm = np.where(isarm[None, :], 0, W); nsum = nonarm.sum(1)
        fb = np.zeros(len(names)); fb[names.index('CC_Base_Spine02')] = 0.7; fb[names.index(f'CC_Base_{side}_Clavicle')] = 0.3
        tor = np.where(nsum[:, None] > 1e-6, nonarm/np.maximum(nsum, 1e-9)[:, None], fb[None, :])
        armd = np.where(orig_arm[:, None] > 1e-6, np.where(isarm[None, :], W, 0)/np.maximum(orig_arm, 1e-9)[:, None], np.eye(len(names))[iu][None, :])
        new = w_tor[:, None]*tor + w_arm[:, None]*armd
        sel = m > 0
        W1[sel, :len(names)] = (1 - m[sel, None])*W[sel] + m[sel, None]*new[sel]
        W1[sel, out_names.index(f'Share_{side}_Upperarm')] = m[sel]*w_sh[sel]
    me = ob.data
    ev = np.empty(len(me.edges)*2, np.int32); me.edges.foreach_get('vertices', ev); ev = ev.reshape(-1, 2)
    deg = np.bincount(ev.ravel(), minlength=len(co))
    diff = np.abs(W1[:, :len(names)] - W).sum(1) + W1[:, len(names):].sum(1)
    reg = smooth_field((diff > 1e-4).astype(float)[:, None], ev, deg, np.ones(len(co)), 6)[:, 0]
    mask = np.clip(reg*3, 0, 1)
    W1 = smooth_field(W1, ev, deg, mask, iters)
    W1 = np.maximum(W1, 0); W1 /= W1.sum(1, keepdims=True)
    return out_names, W1, mask
def write_matrix(ob, names, W, limit=4):
    for g in list(ob.vertex_groups): ob.vertex_groups.remove(g)
    groups = {}
    for i in range(W.shape[0]):
        idx = np.argsort(-W[i])[:limit]; vals = W[i, idx]; tot = vals.sum()
        if tot <= 0: continue
        for j, x in zip(idx, vals/tot):
            if x < 0.01: continue
            n = names[j]
            if n not in groups: groups[n] = ob.vertex_groups.new(name=n)
            groups[n].add([i], round(float(x), 3), 'REPLACE')
    return len(groups)
'''
S['armpit_weights'] = ARMPIT_LIB + r'''
rig = bpy.data.objects['Arjun_Rig']
info = {}
for name in ('Arjun_Shirt_Chambray', 'Arjun_Shirt_Classic'):
    ob = bpy.data.objects[name]
    names, W1, mask = armpit_weights(ob, rig)
    info[name] = {'groups': write_matrix(ob, names, W1), 'region_verts': int((mask > 0).sum())}
bpy.app.driver_namespace['armpit_weights'] = info
'''

# Edits that move vertices of the chambray keep its stored corner normals except at moved vertices.
EDGE_LIB = r'''
import bpy, bmesh, numpy as np
from mathutils import Vector
def corner_normals(me):
    return np.array([l.vector[:] for l in me.corner_normals], np.float32)
def restore_normals(me, cn_attr, moved_v):
    cn = np.empty(len(me.loops)*3, np.float32); me.attributes[cn_attr].data.foreach_get('vector', cn); cn = cn.reshape(-1, 3)
    co = np.empty(len(me.vertices)*3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    vn = np.zeros((len(me.vertices), 3))
    for p in me.polygons:
        vs = list(p.vertices); n = np.zeros(3)
        for k in range(len(vs)):
            n += np.cross(co[vs[k]] - co[vs[0]], co[vs[(k+1) % len(vs)]] - co[vs[0]])
        for v in vs: vn[v] += n
    vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-12)[:, None]
    li = np.empty(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', li)
    mv = np.zeros(len(me.vertices), bool); mv[list(moved_v)] = True
    sel = mv[li]
    cn[sel] = vn[li[sel]]
    me.normals_split_custom_set(cn.tolist()); me.attributes.remove(me.attributes[cn_attr]); me.update()
'''

# The restored neck skin pokes through the tee's neckline in thin ridges (the tee was fitted to the old body).
# For each neck/chest skin vertex (|x| < 0.10 and z 1.30-1.47: the T-posed forearms sit at the same height),
# cast from outside toward the neck axis; where the tee is hit within 2 cm and less than 3 mm outside the
# skin, lift that tee triangle's vertices radially; spread the lift 4 passes. Arjun: 76 hits, 14.8 mm max.
S['tee_neckline'] = EDGE_LIB + r'''
from mathutils.bvhtree import BVHTree
body = bpy.data.objects['Arjun_Body']; bm_ = body.data
regb = np.empty(len(bm_.polygons), np.int32); bm_.attributes['companion_region'].data.foreach_get('value', regb)
skin_v = sorted({vi for i, p in enumerate(bm_.polygons) if regb[i] == 0 for vi in p.vertices})
bco = np.empty(len(bm_.vertices)*3); bm_.vertices.foreach_get('co', bco); bco = bco.reshape(-1, 3)
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
a = me.attributes.get('tmp_cn') or me.attributes.new('tmp_cn', 'FLOAT_VECTOR', 'CORNER')
a.data.foreach_set('vector', corner_normals(me).ravel())
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
tris = [tuple(p.vertices) for p in me.polygons if fm[p.index] == 1]
co = np.empty(len(me.vertices)*3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
tee = BVHTree.FromPolygons([Vector(c) for c in co], tris)
need = np.zeros(len(me.vertices)); hits = 0
for vi in skin_v:
    s = bco[vi]
    if s[2] < 1.30 or s[2] > 1.47 or s[1] > 0.03 or abs(s[0]) > 0.10: continue
    ax = np.array([0.0, 0.02, s[2]]); d = s - ax; rs = np.linalg.norm(d)
    if rs < 1e-6: continue
    d /= rs
    h = tee.ray_cast(Vector(ax + d*0.30), Vector(-d), 0.30)
    if h[0] is None or (np.array(h[0]) - s) @ (np.array(h[0]) - s) > 0.02**2: continue
    rt = np.linalg.norm(np.array(h[0]) - ax)
    if rt < rs + 0.003:
        hits += 1
        for v in tris[h[2]]: need[v] = max(need[v], rs + 0.003 - rt)
ev = np.empty(len(me.edges)*2, np.int32); me.edges.foreach_get('vertices', ev); ev = ev.reshape(-1, 2)
tee_v = np.array(sorted({v for t in tris for v in t}))
istee = np.zeros(len(me.vertices), bool); istee[tee_v] = True
ev = ev[istee[ev[:, 0]] & istee[ev[:, 1]]]
deg = np.bincount(ev.ravel(), minlength=len(me.vertices))
amt = need.copy()
for _ in range(4):
    acc = np.zeros(len(amt)); np.add.at(acc, ev[:, 0], amt[ev[:, 1]]); np.add.at(acc, ev[:, 1], amt[ev[:, 0]])
    amt = np.maximum(need, 0.5*amt + 0.5*np.where(deg > 0, acc/np.maximum(deg, 1), 0))
moved = set(int(v) for v in tee_v if co[v][2] > 1.28 and co[v][1] < 0.03)
for vi in tee_v:
    if amt[vi] <= 1e-5: continue
    p = co[vi]; ax = np.array([0.0, 0.02, p[2]]); d = p - ax; r = np.linalg.norm(d)
    co[vi] = ax + d/r*(r + amt[vi])
me.vertices.foreach_set('co', co.ravel()); me.update()
restore_normals(me, 'tmp_cn', moved)
bpy.app.driver_namespace['tee_push4'] = {'skin_hits': hits, 'max_mm': round(float(amt.max())*1000, 1)}
'''
# (Run once before 'tee_neckline': the same radial push per tee vertex, 3.5 mm off the skin, smoothed 12
# passes, cleared the broad cases. It is the 13e 'tee_restore_push' without the restore.)

# Collar and lapel edges of the chambray: drop dangling "ear" triangles at the right lapel's torn end,
# smooth its open edge (6 passes) and relax the crumpled strip behind it (Taubin 0.5/-0.53 x6).
S['lapel_end'] = EDGE_LIB + r'''
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
a = me.attributes.get('tmp_cn') or me.attributes.new('tmp_cn', 'FLOAT_VECTOR', 'CORNER')
a.data.foreach_set('vector', corner_normals(me).ravel())
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
fabric = lambda f: f.material_index == 0
def in_r1(co): return -0.07 < co.x < -0.005 and 1.33 < co.z < 1.43 and co.y < -0.02
for _ in range(4):
    kill = [f for f in bm.faces if fabric(f) and all(in_r1(v.co) for v in f.verts) and sum(1 for e in f.edges if e.is_boundary) >= 2]
    if not kill: break
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.verts.index_update(); bm.faces.index_update(); bm.verts.ensure_lookup_table()
moved = set()
bnd = [v for v in bm.verts if v.is_boundary and in_r1(v.co) and any(fabric(f) for f in v.link_faces)]
for _ in range(6):
    new = {}
    for v in bnd:
        nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
        if len(nb) != 2: continue
        new[v] = v.co + 0.5*((nb[0].co + nb[1].co)/2 - v.co)
    for v, c in new.items(): v.co = c; moved.add(v)
strip = [v for v in bm.verts if not v.is_boundary and -0.075 < v.co.x < -0.0 and 1.33 < v.co.z < 1.44 and v.co.y < -0.02 and all(fabric(f) for f in v.link_faces)]
for lam in (0.5, -0.53)*6:
    new = {}
    for v in strip:
        nb = [e.other_vert(v) for e in v.link_edges]
        if not nb: continue
        c = sum((u.co for u in nb), Vector()) / len(nb)
        new[v] = v.co + lam*(c - v.co)
    for v, c in new.items(): v.co = c; moved.add(v)
bm.verts.index_update()
moved_idx = {v.index for v in moved if v.is_valid}
bm.to_mesh(me); bm.free(); me.update()
restore_normals(me, 'tmp_cn', moved_idx)
'''

# The collar's top edge at the back is a saw-tooth (the colour boundary against the hair, cut along
# triangles); on blue fabric it shows the hair's matching teeth. Raise the notches to straight lines between
# the tooth tips (never lower: below the collar the back of the neck is open), at most 18 mm, within
# 100 degrees of the back; lift the first inner ring by half.
S['collar_tips'] = EDGE_LIB + r'''
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
a = me.attributes.get('tmp_cn') or me.attributes.new('tmp_cn', 'FLOAT_VECTOR', 'CORNER')
a.data.foreach_set('vector', corner_normals(me).ravel())
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
fabric = lambda f: f.material_index == 0
def angle(v): return np.degrees(np.arctan2(v.co.x, v.co.y - 0.02))
top = [v for v in bm.verts if v.is_boundary and v.co.z > 1.44 and v.co.y > -0.03 and abs(angle(v)) <= 100 and any(fabric(f) for f in v.link_faces)]
topset = set(top)
tips = []
for v in top:
    nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary and e.other_vert(v) in topset]
    if len(nb) == 2 and v.co.z >= max(u.co.z for u in nb): tips.append(v)
ta = np.array([angle(v) for v in tips]); tz = np.array([v.co.z for v in tips]); o = np.argsort(ta); ta, tz = ta[o], tz[o]
moved = set()
for v in top:
    a0 = angle(v)
    if a0 < ta[0] or a0 > ta[-1]: continue
    dz = min(float(np.interp(a0, ta, tz)) - v.co.z, 0.018)
    if dz > 1e-4:
        v.co.z += dz; moved.add(v)
        for e in v.link_edges:
            u = e.other_vert(v)
            if not u.is_boundary and u.co.z > 1.42: u.co.z += 0.5*dz; moved.add(u)
bm.verts.index_update()
moved_idx = {v.index for v in moved}
bm.to_mesh(me); bm.free(); me.update()
restore_normals(me, 'tmp_cn', moved_idx)
'''

# Loose flaps where the collar meets the neck at the sides and at the lapel tops: drop ear triangles more
# than 70 degrees from the back (behind it they are the tooth tips that now carry the smooth edge).
S['collar_flaps'] = EDGE_LIB + r'''
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
a = me.attributes.get('tmp_cn') or me.attributes.new('tmp_cn', 'FLOAT_VECTOR', 'CORNER')
a.data.foreach_set('vector', corner_normals(me).ravel())
bm = bmesh.new(); bm.from_mesh(me)
def ang(c): return abs(np.degrees(np.arctan2(c.x, c.y - 0.02)))
for _ in range(3):
    kill = []
    for f in bm.faces:
        if sum(1 for e in f.edges if e.is_boundary) < 2: continue
        c = f.calc_center_median()
        if c.z > 1.38 and abs(c.x) < 0.11 and ang(c) >= 70: kill.append(f)
    if not kill: break
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.verts.index_update()
moved = set()
bnd = [v for v in bm.verts if v.is_boundary and v.co.z > 1.38 and abs(v.co.x) < 0.11 and ang(v.co) >= 70]
for _ in range(3):
    new = {}
    for v in bnd:
        nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
        if len(nb) != 2: continue
        new[v] = v.co + 0.3*((nb[0].co + nb[1].co)/2 - v.co)
    for v, c in new.items(): v.co = c; moved.add(v)
bm.verts.index_update()
moved_idx = {v.index for v in moved}
bm.to_mesh(me); bm.free(); me.update()
restore_normals(me, 'tmp_cn', moved_idx)
'''

# ==================================================================================================
# ---- Superseded attempts (kept so the pitfalls table can point at them) ----
# restore_lining_uv: per-loop nearest-point UVs smeared across chart seams
S['restore_lining_uv'] = r'''
import numpy as np, bmesh, mathutils
from mathutils.bvhtree import BVHTree
src_path = 'D:/Blender/Companion_Outfits_20261009_Arjun/checkpoints/arjun-outfits-before-split.blend'
with bpy.data.libraries.load(src_path, link=False) as (df, dt):
    dt.meshes = ['Arjun_Body']
orig = dt.meshes[0]; orig.name = 'orig_body_ref'
orig.calc_loop_triangles()
reg = np.empty(len(orig.polygons), np.int32); orig.attributes['companion_region'].data.foreach_get('value', reg)
tris = [t for t in orig.loop_triangles if reg[t.polygon_index] == 3]
verts = [v.co.copy() for v in orig.vertices]
bvh = BVHTree.FromPolygons(verts, [tuple(t.vertices) for t in tris])
uvd = orig.uv_layers.active.data
ob = bpy.data.objects['Arjun_Shirt_Classic']
bm = bmesh.new(); bm.from_mesh(ob.data)
uvl = bm.loops.layers.uv.active; rl = bm.faces.layers.int.get('companion_region')
n = 0; worst = 0.0
for f in bm.faces:
    if f[rl] != 10: continue
    for l in f.loops:
        p = l.vert.co
        loc, nrm, idx, d = bvh.find_nearest(p)
        worst = max(worst, d)
        t = tris[idx]
        a, b, c = (orig.vertices[i].co for i in t.vertices)
        w = mathutils.geometry.barycentric_transform(loc, a, b, c, mathutils.Vector((1,0,0)), mathutils.Vector((0,1,0)), mathutils.Vector((0,0,1)))
        uv = uvd[t.loops[0]].uv*w.x + uvd[t.loops[1]].uv*w.y + uvd[t.loops[2]].uv*w.z
        l[uvl].uv = uv
        n += 1
bm.to_mesh(ob.data); bm.free()
bpy.data.meshes.remove(orig)
bpy.app.driver_namespace['lining_uv'] = {'loops': n, 'worst_dist': worst}
'''

# chambray_cut: cut through the folded placket; the strips dangled
S['chambray_cut'] = r'''
import bmesh, numpy as np
ob = bpy.data.objects['Arjun_Shirt_Chambray']
X_CUT = 0.010
bm = bmesh.new(); bm.from_mesh(ob.data)
faces = [f for f in bm.faces if (lambda c: c.y < -0.04 and 0.80 < c.z < 1.36 and abs(c.x - X_CUT) < 0.03)(f.calc_center_median())]
geom = list({v for f in faces for v in f.verts}) + list({e for f in faces for e in f.edges}) + faces
res = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=(X_CUT,0,0), plane_no=(1,0,0))
cut = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
# keep only edges lying on the plane inside the front window
cut = [e for e in cut if all(abs(v.co.x - X_CUT) < 1e-5 for v in e.verts) and all(v.co.y < -0.04 and v.co.z < 1.345 for v in e.verts)]
bmesh.ops.split_edges(bm, edges=cut)
bm.to_mesh(ob.data); bm.free(); ob.data.update()
# report boundary loops count
bm = bmesh.new(); bm.from_mesh(ob.data)
bnd = [e for e in bm.edges if e.is_boundary]
bpy.app.driver_namespace['chambray_cut'] = {'cut_edges': len(cut), 'boundary_edges': len(bnd)}
bm.free()
'''

# chambray_open: per-vertex ray casts failed through button holes and left spikes
S['chambray_open'] = r'''
import bmesh, numpy as np, mathutils
from mathutils.bvhtree import BVHTree
X_CUT = 0.010; YC = 0.0
src = bpy.data.objects['Arjun_Shirt_Classic']
sm = src.data
sreg = np.empty(len(sm.polygons), np.int32); sm.attributes['companion_region'].data.foreach_get('value', sreg)
shell = BVHTree.FromPolygons([v.co.copy() for v in sm.vertices], [tuple(p.vertices) for i,p in enumerate(sm.polygons) if sreg[i] == 2])
def r_outer(theta, z):
    d = mathutils.Vector((np.sin(theta), -np.cos(theta), 0.0)); o = mathutils.Vector((0.0, YC, z))
    hit = shell.ray_cast(o + d*0.45, -d, 0.45)
    return None if hit[0] is None else (hit[0] - o).length
def interp(z, zs, vs):
    return float(np.interp(z, zs, vs))
ZS = [0.80, 0.90, 1.00, 1.30, 1.36, 1.42, 1.46]
DM = [28, 28, 25, 22, 16, 5, 0]   # minus (button) panel, degrees
DP = [21, 21, 18, 15, 11, 4, 0]   # plus (pocket) panel
ob = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ob.data); bm.verts.ensure_lookup_table()
moved = 0; failed = 0; maxd = 0.0
new = {}
for v in bm.verts:
    p = v.co
    if p.y > 0.06 or p.z < 0.80 or p.z > 1.47: continue
    fc = [f.calc_center_median().x for f in v.link_faces]
    side = 1 if (sum(fc)/len(fc)) > X_CUT else -1
    r_v = np.hypot(p.x, p.y - YC); th = np.arctan2(p.x, -(p.y - YC))
    r_s = np.hypot(X_CUT, 0.15); th_cut = np.arctan2(X_CUT, 0.15)
    phi = np.degrees(side*(th - th_cut))
    if phi < -2: continue
    phi1 = float(np.interp(p.z, [0.80, 1.15, 1.30, 1.46], [140, 140, 95, 80]))
    t = np.clip((phi - 8.0)/(phi1 - 8.0), 0, 1); w = 1 - (0.7*t + 0.3*t*t*(3 - 2*t))
    if w <= 0: continue
    dz = interp(p.z, ZS, DM if side < 0 else DP)
    dth = np.radians(side*dz*w)
    rs0 = r_outer(th, p.z); rs1 = r_outer(th + dth, p.z)
    if rs0 is None or rs1 is None:
        failed += 1; continue
    off = r_v - rs0
    r_new = rs1 + off
    th2 = th + dth
    q = mathutils.Vector((r_new*np.sin(th2), YC - r_new*np.cos(th2), p.z))
    maxd = max(maxd, (q - p).length)
    new[v.index] = q
for i, q in new.items(): bm.verts[i].co = q
moved = len(new)
bm.to_mesh(ob.data); bm.free(); ob.data.update()
bpy.app.driver_namespace['chambray_open'] = {'moved': moved, 'failed': failed, 'max_move': maxd}
'''

# chambray_open2: first smooth-shell version, cut at the button line
S['chambray_open2'] = r'''
import bmesh, numpy as np, mathutils
X_CUT = 0.010; YC = 0.0
TH, ZZ, R = bpy.app.driver_namespace['shell_grid']
def Rs(t, z):
    fi = np.clip((z - ZZ[0])/(ZZ[1]-ZZ[0]), 0, len(ZZ)-1.001); fj = np.clip((t - TH[0])/(TH[1]-TH[0]), 0, len(TH)-1.001)
    i0 = int(fi); j0 = int(fj); a = fi - i0; b = fj - j0
    return (R[i0,j0]*(1-a)*(1-b) + R[i0+1,j0]*a*(1-b) + R[i0,j0+1]*(1-a)*b + R[i0+1,j0+1]*a*b)
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]
DM = [28, 28, 25, 22, 16, 5, 0]
DP = [21, 21, 18, 15, 11, 4, 0]
ob = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ob.data); bm.verts.ensure_lookup_table()
new = {}; maxd = 0.0
th_cut = np.arctan2(X_CUT, 0.15)
for v in bm.verts:
    p = v.co
    if p.y > 0.06 or p.z < 0.78 or p.z > 1.47: continue
    fc = [f.calc_center_median().x for f in v.link_faces]
    side = 1 if (sum(fc)/len(fc)) > X_CUT else -1
    r_v = np.hypot(p.x, p.y - YC); th = np.arctan2(p.x, -(p.y - YC))
    phi = np.degrees(side*(th - th_cut))
    if phi < -3: continue
    phi1 = float(np.interp(p.z, [0.78, 1.15, 1.30, 1.47], [140, 140, 95, 80]))
    t = np.clip((phi - 8.0)/(phi1 - 8.0), 0, 1); w = 1 - (0.7*t + 0.3*t*t*(3 - 2*t))
    if w <= 0: continue
    dth = np.radians(side*float(np.interp(p.z, ZS, DM if side < 0 else DP))*w)
    r_new = r_v + (Rs(th + dth, p.z) - Rs(th, p.z))
    th2 = th + dth
    q = mathutils.Vector((r_new*np.sin(th2), YC - r_new*np.cos(th2), p.z))
    maxd = max(maxd, (q - p).length); new[v.index] = q
for i, q in new.items(): bm.verts[i].co = q
bm.to_mesh(ob.data); bm.free(); ob.data.update()
bpy.app.driver_namespace['chambray_open'] = {'moved': len(new), 'max_move': maxd}
'''

# chambray_open3: wider opening with the old cut
S['chambray_open3'] = r'''
import bmesh, numpy as np, mathutils
X_CUT = 0.010; YC = 0.0
TH, ZZ, R = bpy.app.driver_namespace['shell_grid']
def Rs(t, z):
    fi = np.clip((z - ZZ[0])/(ZZ[1]-ZZ[0]), 0, len(ZZ)-1.001); fj = np.clip((t - TH[0])/(TH[1]-TH[0]), 0, len(TH)-1.001)
    i0 = int(fi); j0 = int(fj); a = fi - i0; b = fj - j0
    return (R[i0,j0]*(1-a)*(1-b) + R[i0+1,j0]*a*(1-b) + R[i0,j0+1]*(1-a)*b + R[i0+1,j0+1]*a*b)
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]
DM = [29, 29, 27, 27, 20, 6, 0]
DP = [22, 22, 20, 18, 13, 4, 0]
ob = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ob.data); bm.verts.ensure_lookup_table()
new = {}; maxd = 0.0
th_cut = np.arctan2(X_CUT, 0.15)
for v in bm.verts:
    p = v.co
    if p.y > 0.06 or p.z < 0.78 or p.z > 1.47: continue
    fc = [f.calc_center_median().x for f in v.link_faces]
    side = 1 if (sum(fc)/len(fc)) > X_CUT else -1
    r_v = np.hypot(p.x, p.y - YC); th = np.arctan2(p.x, -(p.y - YC))
    phi = np.degrees(side*(th - th_cut))
    if phi < -3: continue
    phi1 = float(np.interp(p.z, [0.78, 1.15, 1.30, 1.47], [140, 140, 95, 80]))
    t = np.clip((phi - 8.0)/(phi1 - 8.0), 0, 1); w = 1 - (0.7*t + 0.3*t*t*(3 - 2*t))
    if w <= 0: continue
    dth = np.radians(side*float(np.interp(p.z, ZS, DM if side < 0 else DP))*w)
    r_new = r_v + (Rs(th + dth, p.z) - Rs(th, p.z))
    th2 = th + dth
    q = mathutils.Vector((r_new*np.sin(th2), YC - r_new*np.cos(th2), p.z))
    maxd = max(maxd, (q - p).length); new[v.index] = q
for i, q in new.items(): bm.verts[i].co = q
bm.to_mesh(ob.data); bm.free(); ob.data.update()
bpy.app.driver_namespace['chambray_open'] = {'moved': len(new), 'max_move': maxd}
'''

# waist_ext: walked a multi-loop jagged boundary and created thousands of junk faces
S['waist_ext'] = r'''
import numpy as np, bmesh, mathutils
BZ = [0.95, 1.00, 1.10, 1.20, 1.30]
BA = [0.150, 0.150, 0.153, 0.160, 0.168]
BF = [0.106, 0.108, 0.114, 0.120, 0.122]
BB = [0.122, 0.122, 0.123, 0.127, 0.132]
def r_body(t, z):
    a = np.interp(z, BZ, BA); b = np.interp(z, BZ, BF) if abs(t) < np.pi/2 else np.interp(z, BZ, BB)
    return 1.0/np.sqrt((np.sin(t)/a)**2 + (np.cos(t)/b)**2)
ob = bpy.data.objects['Arjun_Trousers']
bm = bmesh.new(); bm.from_mesh(ob.data); bm.verts.ensure_lookup_table(); bm.edges.ensure_lookup_table()
# ordered top boundary loop
bedges = [e for e in bm.edges if e.is_boundary and min(v.co.z for v in e.verts) > 0.82]
adj = {}
for e in bedges:
    a, b = e.verts; adj.setdefault(a, []).append(b); adj.setdefault(b, []).append(a)
start = next(iter(adj)); loop = [start]; prev = None; cur = start
while True:
    nxt = [w for w in adj[cur] if w is not prev]
    if not nxt: break
    w = nxt[0]
    if w is start: break
    loop.append(w); prev, cur = cur, w
    if len(loop) > 5000: break
# orient loop by angle
ths = [np.arctan2(v.co.x, -v.co.y) for v in loop]
# make angle increase along the loop
if np.sum(np.diff(np.unwrap(ths))) < 0: loop.reverse(); ths = ths[::-1]
uvl = bm.loops.layers.uv.active
rl = bm.faces.layers.int.get('companion_region')
# smooth the per-vertex angle so rings stay ordered
TH = np.array([np.arctan2(v.co.x, -v.co.y) for v in loop])
RB = np.array([np.hypot(v.co.x, v.co.y) for v in loop])
ZB = np.array([v.co.z for v in loop])
profile = [
    (0.915, 'blend', 0.0), (0.935, 'blend', 0.0), (0.950, 'blend', 0.0), (0.959, 'body', 0.0042),
    (0.961, 'body', 0.0052), (0.979, 'body', 0.0054), (0.997, 'body', 0.0052),   # waistband face
    (0.9995, 'body', 0.0038), (0.9985, 'body', 0.0024), (0.990, 'body', 0.0018), (0.978, 'body', 0.0016)]   # rolled top edge and inner facing
prev_ring = loop
rings = [loop]
for k, (z, mode, off) in enumerate(profile):
    ring = []
    for j, v in enumerate(loop):
        t = TH[j]
        if mode == 'blend':
            s = (z - 0.90)/(0.959 - 0.90); s = s*s*(3 - 2*s)
            r = RB[j]*(1 - s) + (r_body(t, 0.959) + 0.0042)*s
        else:
            r = r_body(t, z) + off
        ring.append(bm.verts.new((r*np.sin(t), -r*np.cos(t), z)))
    rings.append(ring)
n = len(loop); newf = 0
ref_face = loop[0].link_faces[0] if loop[0].link_faces else None
for k in range(len(rings)-1):
    A, B = rings[k], rings[k+1]
    for j in range(n):
        j2 = (j+1) % n
        try:
            f = bm.faces.new((A[j], A[j2], B[j2], B[j]))
        except ValueError:
            continue
        if ref_face is not None: f.material_index = ref_face.material_index
        f[rl] = 11 if k < 4 else 12   # 11 waist extension, 12 waistband
        for l in f.loops: l[uvl].uv = (0.0, 0.0)
        newf += 1
bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f[rl] in (11,12)])
bm.to_mesh(ob.data); bm.free(); ob.data.update()
for p in ob.data.polygons: p.use_smooth = True
bpy.app.driver_namespace['waist_info'] = {'loop': n, 'new_faces': newf, 'loop_z': [float(ZB.min()), float(ZB.max())]}
'''

# waist_ext2: cut at 0.865 m still left front holes
S['waist_ext2'] = r'''
import numpy as np, bmesh, mathutils
BZ = [0.95, 1.00, 1.10, 1.20, 1.30]
BA = [0.150, 0.150, 0.153, 0.160, 0.168]
BF = [0.106, 0.108, 0.114, 0.120, 0.122]
BB = [0.122, 0.122, 0.123, 0.127, 0.132]
def r_body(t, z):
    a = np.interp(z, BZ, BA); b = np.interp(z, BZ, BF) if abs(t) < np.pi/2 else np.interp(z, BZ, BB)
    return 1.0/np.sqrt((np.sin(t)/a)**2 + (np.cos(t)/b)**2)
ob = bpy.data.objects['Arjun_Trousers']
bm = bmesh.new(); bm.from_mesh(ob.data); bm.verts.ensure_lookup_table()
bedges = [e for e in bm.edges if e.is_boundary and min(v.co.z for v in e.verts) > 0.80]
adj = {}
for e in bedges:
    a, b = e.verts; adj.setdefault(a, []).append(b); adj.setdefault(b, []).append(a)
loops = []; used = set()
for s in adj:
    if s in used: continue
    loop = [s]; used.add(s); prev = None; cur = s
    while True:
        nxt = [w for w in adj[cur] if w is not prev and (w not in used or w is s)]
        if not nxt: break
        w = nxt[0]
        if w is s: break
        loop.append(w); used.add(w); prev, cur = cur, w
    loops.append(loop)
loops.sort(key=len, reverse=True)
loop = loops[0]
TH = np.array([np.arctan2(v.co.x, -v.co.y) for v in loop])
if np.sum(np.diff(np.unwrap(TH))) < 0: loop.reverse(); TH = TH[::-1]
RB = np.array([np.hypot(v.co.x, v.co.y) for v in loop])
uvl = bm.loops.layers.uv.active; rl = bm.faces.layers.int.get('companion_region')
REC = 0.0035
profile = [(0.876, 'rec', 0), (0.893, 'rec', 0), (0.910, 'blend', 0), (0.928, 'blend', 0), (0.945, 'blend', 0), (0.959, 'body', 0.0042),
           (0.961, 'body', 0.0052), (0.979, 'body', 0.0054), (0.997, 'body', 0.0052),
           (0.9995, 'body', 0.0038), (0.9985, 'body', 0.0024), (0.990, 'body', 0.0018), (0.978, 'body', 0.0016)]
rings = [loop]
for (z, mode, off) in profile:
    ring = []
    for j, v in enumerate(loop):
        t = TH[j]
        if mode == 'rec':
            r = RB[j] - REC
        elif mode == 'blend':
            s = (z - 0.893)/(0.959 - 0.893); s = s*s*(3 - 2*s)
            r = (RB[j] - REC)*(1 - s) + (r_body(t, 0.959) + 0.0042)*s
        else:
            r = r_body(t, z) + off
        ring.append(bm.verts.new((r*np.sin(t), -r*np.cos(t), z)))
    rings.append(ring)
n = len(loop); newf = 0
mat = loop[0].link_faces[0].material_index
for k in range(len(rings)-1):
    A, B = rings[k], rings[k+1]
    for j in range(n):
        j2 = (j+1) % n
        f = bm.faces.new((A[j], A[j2], B[j2], B[j]))
        f.material_index = mat; f[rl] = 11 if k < 6 else 12
        for l in f.loops: l[uvl].uv = (0.0, 0.0)
        newf += 1
new_faces = [f for f in bm.faces if f[rl] in (11,12)]
# make the new faces face outward (away from the torso axis) except the inner facing rings
for f in new_faces:
    c = f.calc_center_median(); radial = mathutils.Vector((c.x, c.y, 0)).normalized()
    inner = c.z > 0.975 and f.normal.z < -0.5
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(ob.data); bm.free(); ob.data.update()
for p in ob.data.polygons: p.use_smooth = True
bpy.app.driver_namespace['waist_info'] = {'loops': [len(l) for l in loops][:5], 'loop': n, 'new_faces': newf}
'''

# trousers_cut: first cut at 0.865 m
S['trousers_cut'] = r'''
import bmesh, numpy as np
trou = bpy.data.objects['Arjun_Trousers']
bm = bmesh.new(); bm.from_mesh(trou.data)
# fill remaining small holes in the hip band
top_b = [e for e in bm.edges if e.is_boundary and min(v.co.z for v in e.verts) > 0.80]
filled = bmesh.ops.holes_fill(bm, edges=top_b, sides=60)
nf = len(filled['faces'])
geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=(0,0,0.865), plane_no=(0,0,1), clear_outer=True)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bedges = [e for e in bm.edges if e.is_boundary and min(v.co.z for v in e.verts) > 0.80]
deg = {}
for e in bedges:
    for v in e.verts: deg[v] = deg.get(v, 0) + 1
bm.to_mesh(trou.data); bm.free(); trou.data.update()
bpy.app.driver_namespace['trousers_cut'] = {'filled': nf, 'top_boundary_edges': len(bedges), 'bad_degree': sum(1 for d in deg.values() if d != 2)}
'''

# hem_edges: first fold
S['hem_edges'] = r'''
import bmesh, numpy as np, mathutils
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ch.data); bm.verts.ensure_lookup_table()
bm.normal_update()
bedges = [e for e in bm.edges if e.is_boundary]
adj = {}
for e in bedges:
    a, b = e.verts; adj.setdefault(a, []).append(e); adj.setdefault(b, []).append(e)
# main loop: boundary component containing the lowest vertex
low = min(adj.keys(), key=lambda v: v.co.z)
comp = set(); st = [low]
while st:
    u = st.pop()
    if u in comp: continue
    comp.add(u)
    for e in adj[u]:
        w = e.other_vert(u)
        if w not in comp: st.append(w)
main_edges = [e for e in bedges if e.verts[0] in comp and e.verts[1] in comp]
def horiz(e):
    d = e.verts[1].co - e.verts[0].co
    return abs(d.z) < 0.6*max(d.length, 1e-9)
hem = [v for v in comp if v.co.z < 0.935 and any(horiz(e) for e in adj[v])]
th = np.array([np.arctan2(v.co.x, -v.co.y) for v in hem]); zz = np.array([v.co.z for v in hem])
order = np.argsort(th); ths = th[order]; zs = zz[order]
def env(t):
    d = np.abs((ths - t + np.pi) % (2*np.pi) - np.pi)
    m = d < np.radians(5)
    return zs[m].min() if m.any() else None
zenv = np.array([env(t) for t in th])
zsm = np.array([np.mean([zenv[k] for k in range(len(th)) if abs(((th[k]-t+np.pi) % (2*np.pi)) - np.pi) < np.radians(3)]) for t in th])
moved = 0
for v, zt in zip(hem, zsm):
    if zt < v.co.z - 1e-5:
        v.co.z = zt; moved += 1
bm.normal_update()
# fold the open edges (hem and front edges up to the V): wall into the body, then an inner facing
edges = [e for e in main_edges if max(v.co.z for v in e.verts) < 1.345]
tin = {}
for v in {v for e in edges for v in e.verts}:
    acc = mathutils.Vector()
    for f in v.link_faces: acc += f.calc_center_median() - v.co
    n = v.normal
    acc = acc - n*acc.dot(n)
    tin[v] = acc.normalized() if acc.length > 1e-9 else mathutils.Vector((0,0,1))
r1 = bmesh.ops.extrude_edge_only(bm, edges=edges)
new1 = [g for g in r1['geom'] if isinstance(g, bmesh.types.BMVert)]
# map new verts to source verts through the extruded edges
src_of = {}
for g in r1['geom']:
    if isinstance(g, bmesh.types.BMEdge):
        pass
for f in [g for g in r1['geom'] if isinstance(g, bmesh.types.BMFace)]:
    vs = list(f.verts)
    olds = [v for v in vs if v in tin]; news = [v for v in vs if v not in tin]
    for nv in news:
        best = min(olds, key=lambda o: (o.co - nv.co).length)
        src_of[nv] = best
for nv, o in src_of.items():
    nv.co = o.co - o.normal*0.0025
wall_edges = [g for g in r1['geom'] if isinstance(g, bmesh.types.BMEdge) and all(v in src_of for v in g.verts)]
r2 = bmesh.ops.extrude_edge_only(bm, edges=wall_edges)
for f in [g for g in r2['geom'] if isinstance(g, bmesh.types.BMFace)]:
    vs = list(f.verts)
    olds = [v for v in vs if v in src_of]; news = [v for v in vs if v not in src_of]
    for nv in news:
        o1 = min(olds, key=lambda o: (o.co - nv.co).length)
        o = src_of[o1]
        nv.co = o1.co + tin[o]*0.010 - o.normal*0.0005
rl = bm.faces.layers.int.get('companion_region')
for f in [g for g in r1['geom'] + r2['geom'] if isinstance(g, bmesh.types.BMFace)]:
    f[rl] = 13; f.smooth = True
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(ch.data); bm.free(); ch.data.update()
bpy.app.driver_namespace['hem_info'] = {'hem_verts': len(hem), 'moved_down': moved, 'fold_edges': len(edges)}
'''

# hem_edges2: fold with vertex normals
S['hem_edges2'] = r'''
import bmesh, numpy as np, mathutils
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ch.data); bm.verts.ensure_lookup_table()
rl = bm.faces.layers.int.get('companion_region')
old_fold = [f for f in bm.faces if f[rl] == 13]
bmesh.ops.delete(bm, geom=old_fold, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.normal_update()
bedges = [e for e in bm.edges if e.is_boundary]
adj = {}
for e in bedges:
    a, b = e.verts; adj.setdefault(a, []).append(e); adj.setdefault(b, []).append(e)
comps = []; seen = set()
for s in adj:
    if s in seen: continue
    comp = set(); st = [s]
    while st:
        u = st.pop()
        if u in comp: continue
        comp.add(u)
        for e in adj[u]:
            w = e.other_vert(u)
            if w not in comp: st.append(w)
    seen |= comp; comps.append(comp)
comp = max(comps, key=lambda c: sum(1 for v in c if v.co.z < 0.93))
main_edges = [e for e in bedges if e.verts[0] in comp and e.verts[1] in comp]
XM, XP = -0.026, -0.002
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]
DM = [9, 9, 11, 8, 6, 2, 0]; DP = [23, 23, 23, 21, 15, 5, 0]
def th_edge(side, z):
    if side < 0: return np.arctan2(XM, 0.15) - np.radians(np.interp(z, ZS, DM))
    return np.arctan2(XP, 0.15) + np.radians(np.interp(z, ZS, DP))
def is_edge(v):
    t = np.arctan2(v.co.x, -v.co.y)
    return min(abs(t - th_edge(-1, v.co.z)), abs(t - th_edge(1, v.co.z))) < np.radians(4.0)
hem = [v for v in comp if v.co.z < 0.93 and not is_edge(v)]
th = np.array([np.arctan2(v.co.x, -v.co.y) for v in hem]); zz = np.array([v.co.z for v in hem])
def wrapd(a, b): return np.abs((a - b + np.pi) % (2*np.pi) - np.pi)
zenv = np.array([zz[wrapd(th, t) < np.radians(5)].min() for t in th])
zsm = np.array([zenv[wrapd(th, t) < np.radians(3)].mean() for t in th])
moved = 0
for v, zt in zip(hem, zsm):
    if zt < v.co.z - 1e-5: v.co.z = zt; moved += 1
# panel bottom corners: edge verts below the local hem line join it
for v in comp:
    if is_edge(v) and v.co.z < 0.93:
        t = np.arctan2(v.co.x, -v.co.y)
        near = wrapd(th, t) < np.radians(8)
        if near.any():
            zt = zsm[near].min()
            if v.co.z < zt + 0.004: v.co.z = zt
bm.normal_update()
edges = [e for e in main_edges if max(v.co.z for v in e.verts) < 1.345]
tin = {}
for v in {v for e in edges for v in e.verts}:
    acc = mathutils.Vector()
    for f in v.link_faces: acc += f.calc_center_median() - v.co
    n = v.normal; acc = acc - n*acc.dot(n)
    tin[v] = acc.normalized() if acc.length > 1e-9 else mathutils.Vector((0,0,1))
r1 = bmesh.ops.extrude_edge_only(bm, edges=edges)
src_of = {}
for f in [g for g in r1['geom'] if isinstance(g, bmesh.types.BMFace)]:
    vs = list(f.verts); olds = [v for v in vs if v in tin]; news = [v for v in vs if v not in tin]
    for nv in news: src_of[nv] = min(olds, key=lambda o: (o.co - nv.co).length)
for nv, o in src_of.items(): nv.co = o.co - o.normal*0.0025
wall_edges = [g for g in r1['geom'] if isinstance(g, bmesh.types.BMEdge) and all(v in src_of for v in g.verts)]
r2 = bmesh.ops.extrude_edge_only(bm, edges=wall_edges)
for f in [g for g in r2['geom'] if isinstance(g, bmesh.types.BMFace)]:
    vs = list(f.verts); olds = [v for v in vs if v in src_of]; news = [v for v in vs if v not in src_of]
    for nv in news:
        o1 = min(olds, key=lambda o: (o.co - nv.co).length); o = src_of[o1]
        nv.co = o1.co + tin[o]*0.010 - o.normal*0.0005
for f in [g for g in r1['geom'] + r2['geom'] if isinstance(g, bmesh.types.BMFace)]:
    f[rl] = 13; f.smooth = True
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(ch.data); bm.free(); ch.data.update()
bpy.app.driver_namespace['hem_info'] = {'hem_verts': len(hem), 'moved_down': moved, 'fold_edges': len(edges), 'loops': [len(c) for c in comps][:6]}
'''

# hem_edges3: spiky folds at kinks
S['hem_edges3'] = r'''
import bmesh, numpy as np, mathutils
ch = bpy.data.objects['Arjun_Shirt_Chambray']
bm = bmesh.new(); bm.from_mesh(ch.data); bm.verts.ensure_lookup_table()
rl = bm.faces.layers.int.get('companion_region')
old_fold = [f for f in bm.faces if f[rl] == 13]
bmesh.ops.delete(bm, geom=old_fold, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.normal_update()
bedges = [e for e in bm.edges if e.is_boundary]
adj = {}
for e in bedges:
    a, b = e.verts; adj.setdefault(a, []).append(e); adj.setdefault(b, []).append(e)
comps = []; seen = set()
for s in adj:
    if s in seen: continue
    comp = set(); st = [s]
    while st:
        u = st.pop()
        if u in comp: continue
        comp.add(u)
        for e in adj[u]:
            w = e.other_vert(u)
            if w not in comp: st.append(w)
    seen |= comp; comps.append(comp)
comp = max(comps, key=lambda c: sum(1 for v in c if v.co.z < 0.93))
main_edges = [e for e in bedges if e.verts[0] in comp and e.verts[1] in comp]
XM, XP = -0.026, -0.002
ZS = [0.78, 0.90, 1.00, 1.30, 1.36, 1.42, 1.47]
DM = [9, 9, 11, 8, 6, 2, 0]; DP = [23, 23, 23, 21, 15, 5, 0]
def th_edge(side, z):
    if side < 0: return np.arctan2(XM, 0.15) - np.radians(np.interp(z, ZS, DM))
    return np.arctan2(XP, 0.15) + np.radians(np.interp(z, ZS, DP))
def is_edge(v):
    t = np.arctan2(v.co.x, -v.co.y)
    return min(abs(t - th_edge(-1, v.co.z)), abs(t - th_edge(1, v.co.z))) < np.radians(4.0)
hem = [v for v in comp if v.co.z < 0.93 and not is_edge(v)]
th = np.array([np.arctan2(v.co.x, -v.co.y) for v in hem]); zz = np.array([v.co.z for v in hem])
def wrapd(a, b): return np.abs((a - b + np.pi) % (2*np.pi) - np.pi)
zenv = np.array([zz[wrapd(th, t) < np.radians(5)].min() for t in th])
zsm = np.array([zenv[wrapd(th, t) < np.radians(3)].mean() for t in th])
moved = 0
for v, zt in zip(hem, zsm):
    if zt < v.co.z - 1e-5: v.co.z = zt; moved += 1
# panel bottom corners: edge verts below the local hem line join it
for v in comp:
    if is_edge(v) and v.co.z < 0.93:
        t = np.arctan2(v.co.x, -v.co.y)
        near = wrapd(th, t) < np.radians(8)
        if near.any():
            zt = zsm[near].min()
            if v.co.z < zt + 0.004: v.co.z = zt
band = [v for v in bm.verts if not v.is_boundary and v.co.z < 0.93 and v.co.y > -1 and any(w.is_boundary and w in comp for e in v.link_edges for w in e.verts) ]
ring2 = set(band)
for v in band:
    for e in v.link_edges:
        w = e.other_vert(v)
        if not w.is_boundary and w.co.z < 0.95: ring2.add(w)
for it in range(4):
    bmesh.ops.smooth_vert(bm, verts=list(ring2), factor=0.5, use_axis_x=False, use_axis_y=False, use_axis_z=True)
bm.normal_update()
edges = [e for e in main_edges if max(v.co.z for v in e.verts) < 1.345]
tin = {}
for v in {v for e in edges for v in e.verts}:
    acc = mathutils.Vector()
    for f in v.link_faces: acc += f.calc_center_median() - v.co
    n = v.normal; acc = acc - n*acc.dot(n)
    tin[v] = acc.normalized() if acc.length > 1e-9 else mathutils.Vector((0,0,1))
nb = {}
for e in edges:
    a, b = e.verts; nb.setdefault(a, []).append(b); nb.setdefault(b, []).append(a)
for it in range(3):
    new_t = {}
    for v, t in tin.items():
        acc = t*2.0
        for w in nb.get(v, []): acc = acc + tin[w]
        new_t[v] = acc.normalized() if acc.length > 1e-9 else t
    tin = new_t
r1 = bmesh.ops.extrude_edge_only(bm, edges=edges)
src_of = {}
for f in [g for g in r1['geom'] if isinstance(g, bmesh.types.BMFace)]:
    vs = list(f.verts); olds = [v for v in vs if v in tin]; news = [v for v in vs if v not in tin]
    for nv in news: src_of[nv] = min(olds, key=lambda o: (o.co - nv.co).length)
for nv, o in src_of.items(): nv.co = o.co - o.normal*0.0025
wall_edges = [g for g in r1['geom'] if isinstance(g, bmesh.types.BMEdge) and all(v in src_of for v in g.verts)]
r2 = bmesh.ops.extrude_edge_only(bm, edges=wall_edges)
for f in [g for g in r2['geom'] if isinstance(g, bmesh.types.BMFace)]:
    vs = list(f.verts); olds = [v for v in vs if v in src_of]; news = [v for v in vs if v not in src_of]
    for nv in news:
        o1 = min(olds, key=lambda o: (o.co - nv.co).length); o = src_of[o1]
        nv.co = o1.co + tin[o]*0.010 - o.normal*0.0005
for f in [g for g in r1['geom'] + r2['geom'] if isinstance(g, bmesh.types.BMFace)]:
    f[rl] = 13; f.smooth = True
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(ch.data); bm.free(); ch.data.update()
bpy.app.driver_namespace['hem_info'] = {'hem_verts': len(hem), 'moved_down': moved, 'fold_edges': len(edges), 'loops': [len(c) for c in comps][:6]}
'''

# tee_skin_push: nearest-point skin push moved points by up to 5 cm
S['tee_skin_push'] = r'''
import numpy as np, mathutils
from mathutils.bvhtree import BVHTree
body = bpy.data.objects['Arjun_Body']; bm_ = body.data
regb = np.empty(len(bm_.polygons), np.int32); bm_.attributes['companion_region'].data.foreach_get('value', regb)
skin = BVHTree.FromPolygons([v.co.copy() for v in bm_.vertices], [tuple(p.vertices) for i,p in enumerate(bm_.polygons) if regb[i] == 0])
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
tee_v = sorted({vi for p in me.polygons if fm[p.index] == 1 for vi in p.vertices})
pushed = 0; worst = 0.0
for it in range(2):
    for vi in tee_v:
        v = me.vertices[vi]
        if v.co.z < 1.28: continue
        loc, nrm, idx, d = skin.find_nearest(v.co)
        if loc is None or d > 0.05: continue
        s = (v.co - loc).dot(nrm)
        if s < 0.0035:
            worst = max(worst, 0.0035 - s)
            v.co = loc + nrm*0.0035 + (v.co - loc - nrm*s)*0.0
            pushed += 1
me.update()
bpy.app.driver_namespace['tee_push'] = {'pushed': pushed, 'worst_mm': round(worst*1000, 1)}
'''

# w_chambray: kept pre-opening weights; tee poked through with raised arms
S['w_chambray'] = r'''
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
W = read_w(ch)
co = np.array([v.co[:] for v in me.vertices])
bones = bpy.data.objects['Arjun_Rig'].data.bones
hem_ch = {n: np.arctan2(bones[f'Chambray_{n}_01'].head_local.x, -bones[f'Chambray_{n}_01'].head_local.y) for n in ('L','BL','B','BR','R')}
def wrapd(a, b): return abs((a - b + np.pi) % (2*np.pi) - np.pi)
n_edge = n_hem = 0
for i, p in enumerate(co):
    x, y, z = p
    base = strip(W[i], ('Shirt_',))
    t = np.arctan2(x, -y)
    chains = {}
    # front edge chains (3 bones, root 1.12)
    for side, nm in ((-1, 'FR'), (1, 'FL')):
        if y > -0.02 or z > 1.12: continue
        te = th_edge(side, z)
        d = wrapd(t, te)
        fall = 1 - ss(np.radians(14), np.radians(40), d)
        if fall <= 0: continue
        infl = 0.92*ss(1.12, 0.95, z)*fall
        if infl <= 0: continue
        w1 = ss(1.07, 1.00, z); w2 = ss(0.98, 0.90, z)
        parts = {f'Chambray_{nm}_01': 1 - w1, f'Chambray_{nm}_02': w1 - w2, f'Chambray_{nm}_03': w2}
        for k, v in parts.items():
            if v > 0: chains[k] = chains.get(k, 0) + infl*v
        n_edge += 1
    # hem chains (2 bones, root 0.985) on the sides and back
    if z < 0.985:
        angs = dict(hem_ch); angs['FRe'] = th_edge(-1, z); angs['FLe'] = th_edge(1, z)
        dd = sorted(((wrapd(t, a), n) for n, a in angs.items()))[:2]
        (da, na), (db, nb) = dd
        wa = db/(da + db + 1e-9); wb = 1 - wa
        infl = 0.95*ss(0.985, 0.885, z)
        w01 = np.clip(1 - (0.915 - z)/0.07, 0, 1)
        for n, ww in ((na, wa), (nb, wb)):
            if n in ('FRe', 'FLe'): continue   # the front sector belongs to the edge chains above
            chains[f'Chambray_{n}_01'] = chains.get(f'Chambray_{n}_01', 0) + infl*ww*w01
            chains[f'Chambray_{n}_02'] = chains.get(f'Chambray_{n}_02', 0) + infl*ww*(1 - w01)
        n_hem += 1
    tot = sum(chains.values())
    if tot > 0.95:
        chains = {k: v*0.95/tot for k, v in chains.items()}; tot = 0.95
    w = {k: v*(1 - tot) for k, v in base.items()}
    for k, v in chains.items(): w[k] = w.get(k, 0) + v
    W[i] = w
write_w(ch, W)
bpy.app.driver_namespace['w_chambray'] = {'edge_verts': n_edge, 'hem_verts': n_hem}
'''

# w_trousers: blended from a single boundary vertex
S['w_trousers'] = r'''
tr = bpy.data.objects['Arjun_Trousers']; me = tr.data
W = read_w(tr)
co = np.array([v.co[:] for v in me.vertices])
regf = np.empty(len(me.polygons), np.int32); me.attributes['companion_region'].data.foreach_get('value', regf)
ext = set()
for p in me.polygons:
    if regf[p.index] in (11, 12): ext.update(p.vertices)
loop = [i for i in range(len(co)) if abs(co[i,2] - 0.835) < 1e-4 and i not in ext and W[i]]
lt = np.array([np.arctan2(co[i,0], -co[i,1]) for i in loop])
n = 0
for i in ext:
    if abs(co[i,2] - 0.835) < 1e-4 and W[i]: continue
    t = np.arctan2(co[i,0], -co[i,1])
    j = loop[int(np.argmin(np.abs((lt - t + np.pi) % (2*np.pi) - np.pi)))]
    s = ss(0.835, 0.959, co[i,2])
    W[i] = blend(W[j], WBAND, s); n += 1
write_w(tr, W)
bpy.app.driver_namespace['w_trousers'] = {'ext_verts': n, 'loop_verts': len(loop)}
'''

# tee_upper: first inset of the hidden upper tee
S['tee_upper'] = r'''
import numpy as np, mathutils
ch = bpy.data.objects['Arjun_Shirt_Chambray']; me = ch.data
fm = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', fm)
tee_v = sorted({vi for p in me.polygons if fm[p.index] == 1 for vi in p.vertices})
TF = tee_v[0]
def ss(e0, e1, v):
    t = np.clip((v - e0)/(e1 - e0), 0, 1); return t*t*(3 - 2*t)
n = 0
for vi in tee_v:
    if (vi - TF)//144 >= 43: continue          # leave the rib band and neckline alone
    v = me.vertices[vi]; x, y, z = v.co
    if z < 1.20: continue
    w = ss(0.045, 0.075, abs(x))*ss(1.20, 1.26, z)
    if w <= 0: continue
    axis = mathutils.Vector((0.0, 0.02 if z > 1.30 else 0.0, z))
    d = v.co - axis; r = d.length
    if r < 1e-6: continue
    v.co = axis + d*((r - 0.005*w)/r); n += 1
me.update()
bpy.app.driver_namespace['tee_upper'] = n
'''

# exp_chambray_only: partial re-export helper
S['exp_chambray_only'] = r'''
OUTD = 'D:/Blender/Companion_Outfits_20261009_Arjun/export'
rig = bpy.data.objects['Arjun_Rig']
saved = {b.name: b.use_deform for b in rig.data.bones}
def export(path, meshes, keep):
    for b in rig.data.bones:
        b.use_deform = saved[b.name] and keep(b.name)
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects: o.select_set(False)
    objs = [rig] + [bpy.data.objects[n] for n in meshes]
    for o in objs: o.hide_set(False); o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=False, mesh_smooth_type='OFF', use_tspace=False, colors_type='NONE',
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=True,
        armature_nodetype='NULL', bake_anim=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=False, path_mode='STRIP', embed_textures=False)
core = lambda n: n.startswith('CC_Base')
try:
    export(OUTD + '/Wardrobe/Arjun_Shirt_Chambray.fbx', ('Arjun_Shirt_Chambray',), lambda n: core(n) or n.startswith('Chambray_'))
finally:
    for b in rig.data.bones: b.use_deform = saved[b.name]
    rig.data.pose_position = 'POSE'
import os
bpy.app.driver_namespace['exp_fbx'] = {f: os.path.getsize(os.path.join(dp, f)) for dp, dn, fn in os.walk(OUTD) for f in fn if f.endswith('.fbx')}
'''

# dedupe: library loads duplicated 4K images; run after any libraries.load
S['dedupe'] = r'''
import re
def base(n): return re.sub(r'\.\d{3}$', '', n)
for coll in (bpy.data.materials, bpy.data.images):
    for d in list(coll):
        b = base(d.name)
        if b != d.name and b in coll:
            d.user_remap(coll[b])
for d in list(bpy.data.materials):
    if base(d.name) != d.name and d.users == 0: bpy.data.materials.remove(d)
for d in list(bpy.data.images):
    if base(d.name) != d.name and d.users == 0: bpy.data.images.remove(d)
for m in list(bpy.data.meshes):
    if m.users == 0: bpy.data.meshes.remove(m)
bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
# trousers: merge the duplicated bottom slot
tr = bpy.data.objects['Arjun_Trousers']; me = tr.data
import numpy as np
idx = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', idx)
names = [m.name for m in me.materials]
keep = names.index('Arjun_Bottom')
for i, n in enumerate(names):
    if n == 'Arjun_Bottom' and i != keep: idx[idx == i] = keep
me.polygons.foreach_set('material_index', idx)
while len(me.materials) > 1:
    j = [i for i, m in enumerate(me.materials) if i != keep][0]
    idx = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', idx)
    idx[idx > j] -= 1; me.materials.pop(index=j); me.polygons.foreach_set('material_index', idx)
    keep = [m.name for m in me.materials].index('Arjun_Bottom')
bpy.app.driver_namespace['dedupe'] = {'materials': [m.name for m in bpy.data.materials], 'images': [i.name for i in bpy.data.images], 'trousers_mats': [m.name for m in me.materials]}
'''

# reload_top_tex: texture reload helper
S['reload_top_tex'] = r'''
for n in ('Arjun_Chambray_BaseColor', 'Arjun_Chambray_Normal'):
    bpy.data.images[n].reload()
'''

# chambray_reset: delete-and-rebuild helper
S['chambray_reset'] = r'''
ob = bpy.data.objects.get('Arjun_Shirt_Chambray')
if ob is not None:
    me = ob.data; bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
'''

# tee_neckline_dilated: required clearance over a +/-7 mm window of skin rays; the window caught the chest
# below and the neck slope beside each vertex and lifted the whole neckline (26 mm ledge)
# tee_neckline_wide: the same skin-point push without |x| < 0.10 hit the T-posed forearms at neck height
# and threw tee vertices 0.73 m; always bound skin samples to the region being fitted
# collar_envelope: running max of the collar top over +/-5 degrees raised side vertices by up to 84 mm where
# the collar falls toward the front; join tooth tips instead ('collar_tips')
